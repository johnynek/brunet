'Check for driver and remove it if is installed
res = Run("driverhelper.exe status IpopTap")
If res <> "No matching devices found." Then
  Run("driverhelper.exe remove IpopTap.inf IpopTap")
End If

'Check for driver and if it exists, we had an error
res = Run("driverhelper.exe status IpopTap")
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
  ChangeNetworkName = 1 
  For Each subkey In arrSubKeys
    If subkey <> "Descriptions" Then
      path = NETWORK_CONNECTIONS & "\" & subkey & "\Connection\"
      if wshShell.RegRead("HKLM\" & path & "Name") = name Then
        oReg.DeleteKey HKEY_LOCAL_MACHINE, path
        path = NETWORK_CONNECTIONS & "\" & subkey
        oReg.DeleteKey HKEY_LOCAL_MACHINE, path
      End If
    End If
  Next
RemoveInstances = 0
End Function
