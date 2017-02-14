using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Security.Permissions;
using Microsoft.Win32.SafeHandles;
using System.Runtime.ConstrainedExecution;
using System.Security;


namespace Synapse.Handlers.Legacy.SQLCommand
{
    public class Impersonator
    {
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool LogonUser(String lpszUsername, String lpszDomain, String lpszPassword,
            int dwLogonType, int dwLogonProvider, out SafeTokenHandle phToken);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public extern static bool CloseHandle(IntPtr handle);

        public WindowsIdentity windowsIdentity;
        public WindowsImpersonationContext impersonatedUser;
        SafeTokenHandle safeTokenHandle;

        private string _domain;
        private string _username;
        private string _password;

        [PermissionSetAttribute(SecurityAction.Demand, Name = "FullTrust")]
        public Impersonator()
        {
            windowsIdentity = WindowsIdentity.GetCurrent();
        }

        [PermissionSetAttribute(SecurityAction.Demand, Name = "FullTrust")]
        public Impersonator(String username, String password)
        {
            _username = username;
            _password = password;
            Logon();
        }

        [PermissionSetAttribute(SecurityAction.Demand, Name = "FullTrust")]
        public Impersonator(String domain, String username, String password)
        {
            _domain = domain;
            _username = username;
            _password = password;
            Logon();
        }

        [PermissionSetAttribute(SecurityAction.Demand, Name = "FullTrust")]
        public Impersonator(WindowsIdentity winId)
        {
            windowsIdentity = winId;
        }

        ~Impersonator()
        {
            if (impersonatedUser != null)
                impersonatedUser.Dispose();

            safeTokenHandle.Dispose();
        }

        private void Logon()
        {
            const int LOGON32_PROVIDER_DEFAULT = 0;
            const int LOGON32_LOGON_INTERACTIVE = 2;

            if (impersonatedUser != null)
                StopImpersonation();

            // Call LogonUser to obtain a handle to an access token. 
            bool returnValue = LogonUser(_username, _domain, _password, LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT, out safeTokenHandle);

            if (false == returnValue)
            {
                int ret = Marshal.GetLastWin32Error();
                Console.WriteLine("LogonUser failed with error code : {0}", ret);
                throw new System.ComponentModel.Win32Exception(ret);
            }

            windowsIdentity = new WindowsIdentity(safeTokenHandle.DangerousGetHandle());
        }

        [PermissionSetAttribute(SecurityAction.Demand, Name = "FullTrust")]
        public void StartImpersonation()
        {
            impersonatedUser = windowsIdentity.Impersonate();
        }

        public void StopImpersonation()
        {
            impersonatedUser.Dispose();
            impersonatedUser = null;
//            windowsIdentity = null;
        }

        public static WindowsIdentity WhoAmI()
        {
            return WindowsIdentity.GetCurrent();
        }

    }

    public sealed class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeTokenHandle()
            : base(true)
        {
        }

        [DllImport("kernel32.dll")]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        protected override bool ReleaseHandle()
        {
            return CloseHandle(handle);
        }
    }

}
