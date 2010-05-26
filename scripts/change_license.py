#!/usr/bin/env python

import sys, tempfile, os, shutil

def replace_license(filename, newlicense, oldlicenseid):
  sourcef = open(filename)
  (tempf, tempn) = tempfile.mkstemp()

  oldlicense_found = False
  license_written = False
  endcpr_found = False

  for line in sourcef:
    if not oldlicense_found and not license_written:
      oldlicense_found = line.startswith(oldlicenseid)
      if not oldlicense_found:
        os.write(tempf, line)
    elif oldlicense_found and not license_written:
      license_written = True
      os.write(tempf, newlicense)
      os.write(tempf, "*/\n")
    elif license_written and not endcpr_found:
      endcpr_found = line.startswith("*/")
    else:
      os.write(tempf, line)

  os.close(tempf)
  sourcef.close()
  shutil.move(tempn, filename)

if __name__ == "__main__":
  licensef = open(sys.argv[1])
  oldlicenseid = sys.argv[2]
  newlicense = licensef.read()
  licensef.close()
  for fn in sys.argv[3:]:
    replace_license(fn, newlicense, oldlicenseid)
