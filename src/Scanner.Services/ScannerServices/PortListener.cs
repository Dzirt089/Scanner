using Scanner.Abstractions.Models;

using System.IO.Ports;
using System.Text;
using System.Threading.Channels;

namespace Scanner.Services.ScannerServices
{
	public sealed class PortListener : IDisposable
	{
		private readonly SerialPort port;
		private readonly ChannelWriter<ScanLine> writer;

		private readonly object sync = new();
		private readonly StringBuilder buffer = new();

		public DateTime LastReceived { get; private set; } = DateTime.Now;
		public string PortName => port.PortName;


		private Exception? _lastError;
		public Exception? LastError => _lastError;


		private int _hasFaulted;
		public bool HasFaulted => Volatile.Read(ref _hasFaulted) == 1;

		private void MarkFaulted(Exception ex)
		{
			Volatile.Write(ref _hasFaulted, 1);
			_lastError = ex;
		}

		public PortListener(string portName, ChannelWriter<ScanLine> writer)
		{
			this.writer = writer;
			port = new SerialPort(portName)
			{
				BaudRate = 9600,
				Parity = Parity.None,
				DataBits = 8,
				StopBits = StopBits.One,
				Handshake = Handshake.None,
				Encoding = Encoding.ASCII
			};

			port.DataReceived += Port_DataReceived;
		}

		public void Open()
		{
			if (!port.IsOpen)
				port.Open();
		}

		private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			try
			{
				if (!port.IsOpen) return;

				// кол-во байтов данных в буфере приёма от сканнера
				var countBytes = port.BytesToRead;
				if (countBytes < 0) return;

				byte[]? bytes = new byte[countBytes];

				// кол-во прочитанных байтов
				var read = port.Read(bytes, 0, countBytes);
				if (read <= 0) return;

				LastReceived = DateTime.Now;

				// декодирование последовательности массива байтов в строку
				var text = port.Encoding.GetString(bytes, 0, read);

				lock (sync)
				{
					foreach (var ch in text)
					{
						if (ch is '\r' or '\n')
						{
							if (buffer.Length > 0)
							{
								var line = buffer.ToString();
								buffer.Clear();
								writer.TryWrite(new ScanLine(PortName, line));
							}
							continue;
						}
						buffer.Append(ch);
					}
				}
			}
			catch (Exception ex)
			{
				MarkFaulted(ex);
			}
		}

		public void Dispose()
		{
			try { port.DataReceived -= Port_DataReceived; }
			catch (Exception ex) { MarkFaulted(ex); }

			try { if (port.IsOpen) port.Close(); }
			catch (Exception ex) { MarkFaulted(ex); }
			port?.Dispose();
		}
	}
}
