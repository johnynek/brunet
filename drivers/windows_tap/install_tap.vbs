'RunAs Admin!
If IsAdmin() = False Then
  Set objShell = CreateObject("Shell.Application")
  objShell.ShellExecute "wscript.exe", Chr(34) & _
  Wscript.ScriptFullName & Chr(34), "", "runas", 1
  Wscript.Quit 0
End If


'Remove any previous instances and install a fresh version
'Run("cscript uninstall_tap.vbs uac")
driver = ScriptPath() & "Windows_" & GetOSArchitecture()
driverhelper = Chr(34) & driver & "/driverhelper.exe" & Chr(34)
inf = Chr(34) & driver & "/IpopTap.inf" & Chr(34)
Run(driverhelper & " Install " & inf & " IpopTap")

res = Run(driverhelper & " Status IpopTap")
'Make sure the device is installed and that it has the correct name!
If ChangeNetworkName(res, "tapipop") = 0 Then
  Wscript.Echo "Installation Successful!"
Else
  Wscript.Echo "Installation Failed!  Please uninstall and then reinstall again."
End If

'Changes the Network Name given a PnpInstanceID
'Note:  Windows does a poor job of cleanup and often times, uninstalled devices will have
'  the PnpInstanceID of running devices.  Because of this, all devices with a given
'  PnpInstanceId will be renamed
Function ChangeNetworkName(pnpInstanceID, name)
  Const HKEY_LOCAL_MACHINE = &H80000002
  Const NETWORK_CONNECTIONS = "SYSTEM\CurrentControlSet\Control\Network\{4D36E972-E325-11CE-BFC1-08002BE10318}"
  Dim oReg, path
  Set oReg = GetObject("winmgmts:{impersonationLevel=impersonate}!\\.\root\default:StdRegProv")
  oReg.EnumKey HKEY_LOCAL_MACHINE, NETWORK_CONNECTIONS, arrSubKeys
  Set WshShell = WScript.CreateObject("WScript.Shell")
  ChangeNetworkName = 1 
  For Each subkey In arrSubKeys
    path = "HKLM\" & NETWORK_CONNECTIONS & "\" & subkey & "\Connection\"
    'Turn error reporting off
    On Error Goto 0
    On Error Resume Next
    id = wshShell.RegRead(path & "PnpInstanceID")
    If id = pnpInstanceID Then
      wshShell.RegWrite path & "Name", name
      ChangeNetworkName = 0
      Exit For
    End If
  Next
End Function

'Executes the app and returns the first line from StdOut
Function Run(app)
  Dim oExec, WshShell
  Set WshShell = CreateObject("Wscript.Shell")
  Set oExec = WshShell.Exec(app)

  Do While oExec.Status = 0
    Wscript.Sleep 100
  Loop
  Run = OExec.StdOut.ReadLine()
End Function

'Returns 32 or 64 for the OS Architecture
Function GetOSArchitecture()
  GetOSArchitecture = "64"
  On Error Goto 0
  On Error Resume Next
  Set WshShell = WScript.CreateObject("WScript.Shell")
  Const KEY = "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment\PROCESSOR_ARCHITECTURE"
  arch =  WshShell.RegRead(KEY)
  If arch = "x86" Then
    GetOSArchitecture = "32"
  End If
End Function

Function ScriptPath()
  ScriptPath = Left(WScript.ScriptFullName, _
    Len(WScript.ScriptFullName) - Len(WScript.ScriptName))
End Function

Function IsAdmin()
  IF Instr(1, "XP", Run("%comspec% /c ver | findstr XP"), 0) = 0 Then
    IsAdmin = True
    Exit Function
  End If

  'Check for elevated
  IsAdmin = RunCleanExit("%comspec% /c whoami /all | findstr /I /C:""S-1-16-12288""")
End Function

Function RunCleanExit(app)
  RunCleanExit = False
  Set oShell = CreateObject("WScript.Shell")
  Set oExec = oShell.Exec(app)

  Do While oExec.Status = 0
    WScript.Sleep 100
  Loop
  If oExec.ExitCode = 0 Then
    RunCleanExit = True
  End If
End Function
