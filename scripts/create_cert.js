var stdin = WScript.StdIn;
var stdout = WScript.StdOut;
var filesys = new ActiveXObject("Scripting.FileSystemObject");
var WshShell = new ActiveXObject("WScript.Shell");
var email, pcid, name;

if(!filesys.FolderExists("certificates")) {
  stdout.Write("Enter userid (jabberid@host.com):")
  userid = stdin.ReadLine();
  stdout.Write("Enter PCID:");
  pcid = stdin.ReadLine();
  stdout.Write("Enter Name:");
  name = stdin.ReadLine();
  var oExec = WshShell.Exec("SVPNTool.exe cert " + userid + " " + pcid + " \"" + name + "\"");
}

stdout.WriteLine("Certificate created successfully, now run SocialVPN.exe as administrator");
stdout.WriteLine("Press Enter to close this window");
stdin.ReadLine();
