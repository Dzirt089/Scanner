# Scanner

Приложение (WPF) / служба для прослушивания USB-сканеров штрих-кодов, которые в системе видны как **COM-порт**  
(реальный или виртуальный через донгл/конвертер).

Архитектура построена вокруг паттерна **Producer → Consumer**:
- Producer читает байтовый поток из COM и публикует готовые строки в очередь
- Consumer последовательно обрабатывает строки, пишет данные в БД и обновляет UI

---

## Архитектура (в одном рисунке)

COM ports
|
v
SerialScannerHostedService (BackgroundService)

auto-discovery COM ports

watchdog (stale/faulted listener -> recreate)

PortListener per port (SerialPort.DataReceived)
|
v
ScanChannel (System.Threading.Channels)
|
v
ScanProcessingHostedService (BackgroundService)

читает из канала

создает scope

вызывает IScanProcessor

обновляет UI runtime-state
|
v
IScanProcessor -> Repository (Dapper) -> SQL Server
+ PublishScanEvent -> WPF UI (IMessenger)


Фоновые процессы реализованы через `BackgroundService` / `IHostedService`/ `ErrorReportingHostedService` (Generic Host).  
`ExecuteAsync` живёт весь срок службы приложения/хоста. 

---

> В проекте ключевые “стабилизаторы” такие:
- `Channel` — главная очередь и разграничение producer/consumer
- `lock в PortListener` — защита фрейминга и StringBuilder
- `ConcurrentDictionary` — безопасное управление жизненным циклом listeners/state
- `Interlocked` — “однократность” без lock
- `Volatile` — корректная видимость флагов между потоками
- `Mutex` — защита от двойного запуска приложения

## Основные компоненты

### 1) `SerialScannerHostedService` (Producer)

Задачи:
- раз в **N секунд** перечитать список COM-портов в системе
- для каждого порта создать/обновить `PortListener`
- удалить слушатели для исчезнувших портов
- **watchdog**: если порт “залип” (давно нет данных) или слушатель упал — пересоздать listener

Хранение слушателей — в `ConcurrentDictionary<>`, чтобы операции add/update/remove были потокобезопасны.

### 2) `PortListener` (фрейминг по `\r\n`)

`SerialPort.DataReceived` вызывается **не на UI-потоке**, а на “вторичном” потоке. Кроме того, событие не гарантирует, что строка придёт целиком.

Пример: сообщение может прийти кусками:

- `"Hello\r\nWo"`
- `"rld\r\nHow are"`
- `" you?\r\n"`

Без фрейминга вы не сможете понять границы сообщений.  
С фреймингом по `\r\n` получаем три сообщения:

- `"Hello"`
- `"World"`
- `"How are you?"`

`PortListener` именно это и делает:
- читает байты
- декодирует в текст
- собирает буфер
- по `\r`/`\n` выделяет законченные строки
- публикует `ScanLine(port, line)` в `ChannelWriter`

### 3) `ScanChannel` (глобальная очередь сообщений)

Очередь — это `System.Threading.Channels.Channel<T>`:
- потокобезопасно
- хорошо подходит под producer/consumer
- настраивается под количество readers/writers (`SingleReader`, `SingleWriter`) 

> ⚠️ В проекте сейчас `CreateUnbounded`. Это проще, но при перегрузе может расти память. Для “железного” продакшна лучше bounded-канал с backpressure.
> Сделать это, если понадобится.

### 4) `ScanProcessingHostedService` (Consumer)

Задачи:
- читает `ScanLine` из `ScanChannel`
- создает `IServiceScope` (чтобы безопасно использовать scoped-зависимости)
- вызывает `IScanProcessor.ProcessAsync(...)`
- обновляет runtime state для UI

### 5) `ScanProcessor`

Содержит бизнес-логику:
- фильтрует по известным префиксам (`ScannerOptions.KnownPrefixes`)
- парсит формат скана (`TryParseScan` → `column`, `idAuto`)
- обновляет дату в таблице
- получает данные “автоматики”
- публикует событие в UI (`IScanEventSink` → `IMessenger`)

---

## Потоки и UI

- `SerialPort.DataReceived` приходит не на UI-поток → UI трогать нельзя напрямую.
- UI обновляется через `IMessenger` + `Dispatcher` (в `MainViewModel.Receive(...)`).

---

## Логирование (Serilog)

Логи пишутся:
- в файл (`Logs/scanner-*.log`)
- в SQL Server таблицу `tbVKT_PlanAuto_ScannerLogs`

В SQL сохраняется `LogEvent` (JSON) и “быстрые” колонки (`scan_id`, `port`, `column`, `id_auto`) через `AdditionalColumns`.  
Это стандартный подход для Serilog MSSQL sink. :contentReference[oaicite:4]{index=4}

### Рекомендуемые события логов (по смыслу)

Чтобы в логах было видно “что идет дальше, чем слушатель создан”, лучше писать **короткие события** с полями.
И обязательно добавлять корреляцию `scan_id` (один id на обработку одного скана).

### Поиск в SQL (пример)

По порту:

`SELECT TOP 200 *
FROM dbo.tbVKT_PlanAuto_ScannerLogs
WHERE port = 'COM8'
ORDER BY TimeStamp DESC;`

По конкретному scan_id:

`SELECT *
FROM dbo.tbVKT_PlanAuto_ScannerLogs
WHERE scan_id = '...'
ORDER BY TimeStamp ASC;`

Репорт ошибок (почта разработчикам)

любая точка кода (producer/consumer/UI)
   |
   v
IErrorReporter.Report(ex, source)
   |
   v
ErrorReportChannel (Channel<ErrorReport>)
   |
   v
ErrorReportingHostedService (BackgroundService)
   - throttle по signature (мин. интервал 1 мин)
   - отправка письма Sender.SendAsync(...)

Важно понимать:

Глобальные обработчики DispatcherUnhandledException / AppDomain.UnhandledException / TaskScheduler.UnobservedTaskException
срабатывают только для реально необработанных исключений, а не для тех, что уже пойманы try/catch.
Поэтому для рабочих ошибок (например, SQL) репорт надо делать в том месте, где исключение ловится.

Конфигурация

ScannerOptions (appsettings.json)
`{
  "Scanner": {
    "KnownPrefixes": [ "korpus", "montaj", "sila", "uprav", "check" ],
    "PortScanIntervalSeconds": 3,
    "PortStaleSeconds": 15
  }
}`

DbConfiguration
`{
  "ConnectionStrings": {
    "ConnectionString": "Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True;"
  }
}`

### Запуск
Режим WPF (tray)

Приложение поднимает Host при старте

Инициализирует TrayIconService

Может стартовать скрыто (аргумент --minimized)

Автозапуск реализован через HKCU Run

Режим службы (опционально)

Если понадобится “без UI” (headless), проект можно вынести в Worker/Service и использовать Microsoft.Extensions.Hosting.WindowsServices / UseWindowsService.
Документация по запуску .NET как Windows Service:

### Траблшутинг
“Порт есть, но данных нет”

Проверь, что COM-порт действительно тот (драйвер/донгл)

Проверь скорость/настройки порта (BaudRate/Parity/DataBits/StopBits)

“Логи есть только про порты, а про обработку скана нет”

Добавьте события уровня Information в ScanProcessingHostedService и ScanProcessor
(см. “Рекомендуемые события логов” выше)

“Почта не отправляется при SQL ошибке”

Если ошибка ловится try/catch, глобальные unhandled-хэндлеры не сработают
→ нужно вызывать IErrorReporter.Report в catch, где ловишь исключение

Примечания по безопасности и поддержке

Не пишите в логи полные строки скана, если там могут быть персональные/чувствительные данные.
В продакшне лучше хранить “сокращённый” line или только разобранные поля (column, id_auto).

Unbounded channel удобен, но при перегрузе может вырасти память. Для критичных сценариев используйте bounded канал.