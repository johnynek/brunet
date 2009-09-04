'This VBscript extract contents of config.zip file to the current directory
'Usage : cscript groupvpn_prepare.vbs [path\to\config.zip]

args = WScript.Arguments.Count

'Select current directoy
currDir= left(WScript.ScriptFullName,(Len(WScript.ScriptFullName))-(len(WScript.ScriptName)))

If args = 0 then
  ZipFile = currDir & "config.zip"
ElseIf args = 1 then
  ZipFile = WScript.Arguments.Item(0)
Else
  Wscript.Echo "Invalid parameters. Usage : cscript groupvpn_prepare.vbs [path\to\config.zip]"
  Wscript.Quit -1
End If

'Check if ZipFile exists
Set fso = CreateObject("Scripting.FileSystemObject")
If NOT ( fso.FileExists(ZipFile) AND args < 2) Then
  Wscript.Echo "Could not find " &  ZipFile & _
    ". Please copy config.zip to the current directory or " & _
    "provide the path as a parameter to this script (eg: " & _
    "cscript groupvpn_prepare.vbs "& chr(34) &"C:\User\My " & _
    "Downloads\config.zip" & chr(34) & ")"
  Wscript.Quit -1
End If

'Extract the contents of the zip file.
set objShell = CreateObject("Shell.Application")
set FilesInZip=objShell.NameSpace(ZipFile).items

If NOT fso.FolderExists("certificates") Then
   fso.CreateFolder("certificates")
End If

fso.DeleteFile(currDir & "certificates\*")

If fso.FileExists(currDir & "ipop.config") Then
    fso.DeleteFile(currDir & "ipop.config")
End If
If fso.FileExists(currDir & "node.config") Then
    fso.DeleteFile(currDir & "node.config")
End If
If fso.FileExists(currDir & "dhcp.config") Then
    fso.DeleteFile(currDir & "dhcp.config")
End If
If fso.FileExists(currDir & "bootstrap.config") Then
    fso.DeleteFile(currDir & "bootstrap.config")
End If

objShell.NameSpace(currDir & "certificates").CopyHere(FilesInZip)
fso.MoveFile currDir & "certificates\*.config", currDir
Wscript.Echo "Preparation for Group VPN is complete."

Set fso = Nothing
Set objShell = Nothing

