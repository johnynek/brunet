'Remove any previous instances and install a fresh version
Run("uninstall.vbs")
Run("driverhelper.exe Install IpopTap.inf IpopTap")

'Make sure the device is installed and that it has the correct name!
If ChangeNetworkName(res, "IpopTap") = 0 Then
  Wscript.Echo "Installation Successful!"
Else
  Wscript.Echo "Installation Failed!  Please uninstall and then reinstall again."
End If

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
    If subkey <> "Descriptions" Then
      path = "HKLM\" & NETWORK_CONNECTIONS & "\" & subkey & "\Connection\"
      if wshShell.RegRead(path & "PnpInstanceID") = pnpInstanceID Then
        wshShell.RegWrite path & "Name", name
        ChangeNetworkName = 0
      End If
    End If
  Next
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