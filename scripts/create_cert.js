var stdin = WScript.StdIn;
var stdout = WScript.StdOut;
var filesys = new ActiveXObject("Scripting.FileSystemObject");
var WshShell = new ActiveXObject("WScript.Shell");
var email, pcid, name;

if(!filesys.FolderExists("certificates")) {
  stdout.Write("Enter Email:")
  email = stdin.ReadLine();
  stdout.Write("Enter PCID:");
  pcid = stdin.ReadLine();
  stdout.Write("Enter Name:");
  name = stdin.ReadLine();
  var oExec = WshShell.Exec("SVPNTool.exe cert " + email + " " + pcid + " " + name);
}
