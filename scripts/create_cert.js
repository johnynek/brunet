var stdin = WScript.StdIn;
var stdout = WScript.StdOut;
var filesys = new ActiveXObject("Scripting.FileSystemObject");
var WshShell = new ActiveXObject("WScript.Shell");
var email, pcid, name;

if(!filesys.FolderExists("certificates")) {
  stdout.Write("Enter userid (jabberid@host.com): ")
  userid = stdin.ReadLine();
  stdout.Write("Enter PCID (home-pc): ");
  pcid = stdin.ReadLine();
  stdout.Write("Enter Name (Jane Doe): ");
  name = stdin.ReadLine();
  var oExec = WshShell.Run("svpncmd.exe cert " + userid + " " + pcid + 
    " \"" + name + "\"", 0, true);

  if(filesys.FolderExists("certificates")) {
    stdout.WriteLine("Run SocialVPN.exe as administrator");
    stdout.WriteLine("Press Enter to close this window");
    stdin.ReadLine();
  }
}

