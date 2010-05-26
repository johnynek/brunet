'RunAs Admin!
If IsAdmin() = False Then
  Set objShell = CreateObject("Shell.Application")
  'Pass a bogus argument with leading blank space, say [ uac]

  objShell.ShellExecute "wscript.exe", Chr(34) & _
  Wscript.ScriptFullName & Chr(34) & " uac", "", "runas", 1
  Wscript.Quit 0
End If

'Check for driver and remove it if is installed
driver = ScriptPath() & "Windows_" & GetOSArchitecture()
driverhelper = Chr(34) & driver & "/driverhelper.exe" & Chr(34)
inf = Chr(34) & driver & "/IpopTap.inf" & Chr(34)

res = Run(driverhelper & " Status IpopTap")
If res <> "No matching devices found." Then
  Run(driverhelper & " Remove " & inf & " IpopTap")
End If

'Check for driver and if it exists, we had an error
res = Run(driverhelper & " Status IpopTap")
If res <> "No matching devices found." Then
  Wscript.Echo "Error uninstalling devices"
Else
  RemoveInstances("tapipop")
  Wscript.Echo "Uninstallation Complete!"
End If


Function Run(app)
  Dim oExec, WshShell
  Set WshShell = CreateObject("Wscript.Shell")
  Set oExec = WshShell.Exec(app)

  Do While oExec.Status = 0
    Wscript.Sleep 100
  Loop
  Run = OExec.StdOut.ReadLine()
End Function

Function RemoveInstances(name)
  Const HKEY_LOCAL_MACHINE = &H80000002
  Const NETWORK_CONNECTIONS = "SYSTEM\CurrentControlSet\Control\Network\{4D36E972-E325-11CE-BFC1-08002BE10318}"
  Dim oReg, path
  Set oReg = GetObject("winmgmts:\\.\root\default:StdRegProv")
  oReg.EnumKey HKEY_LOCAL_MACHINE, NETWORK_CONNECTIONS, arrSubKeys
  Set WshShell = WScript.CreateObject("WScript.Shell")

  For Each subkey In arrSubKeys
    On Error Goto 0
    On Error Resume Next
    path = NETWORK_CONNECTIONS & "\" & subkey & "\Connection\"
    pname = wshShell.RegRead("HKLM\" & path & "Name")
    If pname = name Then
      oReg.DeleteKey HKEY_LOCAL_MACHINE, path
      path = NETWORK_CONNECTIONS & "\" & subkey
      oReg.DeleteKey HKEY_LOCAL_MACHINE, path
    End If
  Next

  RemoveInstances = 0
End Function

'Returns 32 or 64 for the OS Architecture
Function GetOSArchitecture()
  GetOSArchitecture = "64"
  On Error Resume Next
  On Error Goto 0
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
