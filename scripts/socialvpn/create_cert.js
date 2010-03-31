var stdin = WScript.StdIn;
var stdout = WScript.StdOut;
var filesys = new ActiveXObject("Scripting.FileSystemObject");
var WshShell = new ActiveXObject("WScript.Shell");

if(true) {
  stdout.Write("Enter userid (jabberid@host.com): ")
  var uid = stdin.ReadLine();
  stdout.Write("Enter PCID (home-pc): ");
  var pcid = stdin.ReadLine();
  stdout.WriteLine("Creating certificate...");
  var oExec = WshShell.Run("svpncmd.exe cert " + uid + " " + pcid, 0, true);
}

