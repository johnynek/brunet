"""
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Fondation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
"""

namespace Brunet.Security

import System
import System.IO
import System.Security.Cryptography
import Brunet


def print_usage():
  print """Usage: [mono] certhelper.exe ACTION OPTIONS
Actions:
makecert - makes a new x509 certificate request
  parameters:
    country, organization, organizational_unit, name, email, node_address
    outkey - the path to store the rsa private key [this or inkey is required]
    inkey - the path to an existing rsa key [this or outkey is required]
    outcert - the path to store the x509 certificate request
readcert - reads a x509 certificate or certificate request
  parameters:
    cert - the certificate to read
signcert - signs an x509 certificate request
  parameters:
    incert - the certificate to sign
    outcert - the signed certificate
    cakey - the CAs private key
    cacert - the CAs certificate
"""
  Environment.Exit(-1)

def print_error(msg):
  print msg + "\n"
  print_usage()

def makecert(params as Hash):
  try:
    country = params["country"]
    org = params["organization"]
    orgunit = params["organizational_unit"]
    name = params["name"]
    email = params["email"]
    node_addr = params["node_address"]
    cert_path = params["outcert"]
  except:
    print_error("Missing parameter.")

  if params["inkey"] != null:
    key_file = File.Open(params["inkey"], FileMode.Open)
    blob = array(byte, key_file.Length)
    key_file.Read(blob, 0, blob.Length)
    key_file.Close()
    rsa_pub = RSACryptoServiceProvider()
    rsa_pub.ImportCspBlob(blob)
  elif params["outkey"] != null:
    rsa = RSACryptoServiceProvider(1024)
    rsa_pub = RSACryptoServiceProvider()
    rsa_pub.ImportCspBlob(rsa.ExportCspBlob(false))

    blob = rsa.ExportCspBlob(true)
    key_file = File.Open(params["outkey"], FileMode.Create)
    key_file.Write(blob, 0, blob.Length)
    key_file.Close()
  else:
    raise Exception("Missing parameter [in|out]key")

  cm = CertificateMaker(country, org, orgunit, name, email, rsa_pub, node_addr)

  cert_file = File.Open(cert_path, FileMode.Create)
  cert_file.Write(cm.UnsignedData, 0, cm.UnsignedData.Length)
  cert_file.Close()

def readcert(params as Hash):
  try:
    cert_path = params["cert"]
  except:
    print_usage()
  
  try:
    cert_file = File.Open(cert_path, FileMode.Open)
    blob as (byte) = array(byte, cert_file.Length);
    cert_file.Read(blob, 0, blob.Length)
    cert_file.Close()
    try:
      cert = Certificate(blob)
    except:
      cert = CertificateMaker(blob)
  except e:
    print e
    print_error("Invalid certificate.")

  print cert
    
def signcert(params as Hash):
  try:
    incert_path = params["incert"]
    outcert_path = params["outcert"]
    key_path = params["cakey"]
    cert_path = params["cacert"]
  except:
    print_error("Invalid certificate.")

  try:
    cert_file = File.Open(cert_path, FileMode.Open)
    blob as (byte) = array(byte, cert_file.Length);
    cert_file.Read(blob, 0, blob.Length)
    cert_file.Close()
    try:
      cert = Certificate(blob)
    except:
      cert = CertificateMaker(blob)
  except:
    print_error("Invalid CA Cert")

  try:
    key_file = File.Open(key_path, FileMode.Open)
    blob = array(byte, key_file.Length);
    key_file.Read(blob, 0, blob.Length)
    key_file.Close()
    carsa = RSACryptoServiceProvider()
    carsa.ImportCspBlob(blob)
  except:
    print_error("Invalid CA Key")

  try:
    incert_file = File.Open(incert_path, FileMode.Open)
    blob = array(byte, incert_file.Length);
    incert_file.Read(blob, 0, blob.Length)
    incert_file.Close()
    incert = CertificateMaker(blob)
  except e:
    print e
    print_error("Invalid In-Cert")

  blob = incert.Sign(cert, carsa).X509.RawData

  try:
    outcert_file = File.Open(outcert_path, FileMode.Create)
    outcert_file.Write(blob, 0, blob.Length)
    outcert_file.Close()
  except:
    print_error("Invalid Out-Cert File.")

params = {}
for arg in argv[1:]:
  args = arg.Split(char('='))
  if len(args) > 1:
    val = args[1].Trim(char('"'))
    params.Add(args[0], val)
  else:
    params.Add(args[0], true)

action = ""
try:
  action = argv[0]
except:
  pass

if action == "makecert":
  makecert(params)
elif action == "readcert":
  readcert(params)
elif action == "signcert":
  signcert(params)
else:
  print_usage()
