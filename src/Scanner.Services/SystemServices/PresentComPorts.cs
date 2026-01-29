using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Scanner.Services.SystemServices
{
	/// <summary>
	/// Класс перечисления устройств - это собранный “классический” шаблон SetupAPI enumeration 
	/// + чтение FriendlyName, на базе официальной документации и распространённых P/Invoke практик
	/// </summary>
	public static class PresentComPorts
	{
		/*  
		    GUID_DEVINTERFACE_COMPORT 
			Чтобы перечислять именно COM-интерфейсы, используется интерфейсный 
			GUID GUID_DEVINTERFACE_COMPORT — он официально задокументирован 
			(https://learn.microsoft.com/en-us/windows-hardware/drivers/install/guid-devinterface-comport), 
			и GUID ровно тот, что в коде:
			{86E0D1E0-8089-11D0-9CE4-08003E301F73}.
		 */

		private static Guid ComGuid = new("86E0D1E0-8089-11D0-9CE4-08003E301F73");

		private const int DIGCF_PRESENT = 0x00000002;
		private const int DIGCF_DEVICEINTERFACE = 0x00000010;

		private const int SPDRP_FRIENDLYNAME = 0x0000000C;

		private static readonly Regex ComRegex = new(@"\(COM(\d+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		public static IReadOnlyCollection<string> Get()
		{
			var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			IntPtr h = SetupDiGetClassDevs(ref ComGuid, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
			if (h == new IntPtr(-1))
				throw new Win32Exception(Marshal.GetLastWin32Error(), "SetupDiGetClassDevs failed");

			try
			{
				uint index = 0;

				var ifData = new SP_DEVICE_INTERFACE_DATA();
				ifData.cbSize = Marshal.SizeOf(ifData);

				while (SetupDiEnumDeviceInterfaces(h, IntPtr.Zero, ref ComGuid, index, ref ifData))
				{
					index++;

					// 1) узнаём нужный размер буфера
					uint required = 0;
					SetupDiGetDeviceInterfaceDetail(h, ref ifData, IntPtr.Zero, 0, out required, IntPtr.Zero);

					// 2) получаем detail + devInfo
					IntPtr detailPtr = Marshal.AllocHGlobal((int)required);
					try
					{
						// cbSize для DETAIL_DATA зависит от разрядности (важно!)
						int cbSize = (IntPtr.Size == 8) ? 8 : 6;
						Marshal.WriteInt32(detailPtr, cbSize);

						var devInfo = new SP_DEVINFO_DATA();
						devInfo.cbSize = Marshal.SizeOf(devInfo);

						if (!SetupDiGetDeviceInterfaceDetail(h, ref ifData, detailPtr, required, out _, ref devInfo))
							continue;

						// 3) friendly name
						var buf = new byte[1024];
						if (SetupDiGetDeviceRegistryProperty(h, ref devInfo, SPDRP_FRIENDLYNAME, out _, buf, (uint)buf.Length, out _))
						{
							var friendly = Encoding.Unicode.GetString(buf);
							var zero = friendly.IndexOf('\0');
							if (zero >= 0) friendly = friendly[..zero];

							// В 99% случаев там "... (COM5)"
							var m = ComRegex.Match(friendly);
							if (m.Success)
							{
								var com = "COM" + m.Groups[1].Value;
								result.Add(com);
							}
						}
					}
					finally
					{
						Marshal.FreeHGlobal(detailPtr);
					}
				}

				int err = Marshal.GetLastWin32Error();
				// ERROR_NO_MORE_ITEMS = 259 — нормальное завершение перечисления
				if (err != 0 && err != 259)
					throw new Win32Exception(err, "SetupDiEnumDeviceInterfaces failed");
			}
			finally
			{
				SetupDiDestroyDeviceInfoList(h);
			}

			return result;
		}

		#region PInvoke

		[StructLayout(LayoutKind.Sequential)]
		private struct SP_DEVICE_INTERFACE_DATA
		{
			public int cbSize;
			public Guid InterfaceClassGuid;
			public int Flags;
			public IntPtr Reserved;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct SP_DEVINFO_DATA
		{
			public int cbSize;
			public Guid ClassGuid;
			public int DevInst;
			public IntPtr Reserved;
		}

		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		private static extern IntPtr SetupDiGetClassDevs(
			ref Guid ClassGuid,
			string? Enumerator,
			IntPtr hwndParent,
			int Flags);

		[DllImport("setupapi.dll", SetLastError = true)]
		private static extern bool SetupDiEnumDeviceInterfaces(
			IntPtr DeviceInfoSet,
			IntPtr DeviceInfoData,
			ref Guid InterfaceClassGuid,
			uint MemberIndex,
			ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

		[DllImport("setupapi.dll", SetLastError = true)]
		private static extern bool SetupDiGetDeviceInterfaceDetail(
			IntPtr DeviceInfoSet,
			ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
			IntPtr DeviceInterfaceDetailData,
			uint DeviceInterfaceDetailDataSize,
			out uint RequiredSize,
			IntPtr DeviceInfoData);

		[DllImport("setupapi.dll", SetLastError = true)]
		private static extern bool SetupDiGetDeviceInterfaceDetail(
			IntPtr DeviceInfoSet,
			ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
			IntPtr DeviceInterfaceDetailData,
			uint DeviceInterfaceDetailDataSize,
			out uint RequiredSize,
			ref SP_DEVINFO_DATA DeviceInfoData);

		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		private static extern bool SetupDiGetDeviceRegistryProperty(
			IntPtr DeviceInfoSet,
			ref SP_DEVINFO_DATA DeviceInfoData,
			int Property,
			out uint PropertyRegDataType,
			byte[] PropertyBuffer,
			uint PropertyBufferSize,
			out uint RequiredSize);

		[DllImport("setupapi.dll", SetLastError = true)]
		private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

		#endregion
	}
}
