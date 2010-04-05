/*
Copyright (C) 2009  Kyungyong Lee and David Wolinsky <davidiw@ufl.edu>, University of Florida

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using System.Reflection;

namespace Brunet.Applications {
  public class ConfigurationValidator
  {
    protected bool _failed;
    protected string _message;

    ///<summary> Read xml file and check validity using given xsd file.
    /// brunet and ipop configuration file input type is file name, and
    /// dhcp configuration file content is passed as a string.</summary>
    public bool Validate(string config_path, string xsd_path)
    {
      _failed = false;

      Assembly assem = Assembly.GetExecutingAssembly();
      Stream schema_stream = assem.GetManifestResourceStream(xsd_path);
      XmlSchema test_schema = XmlSchema.Read(schema_stream, null);

      XmlReaderSettings settings = new XmlReaderSettings();
      settings.ValidationType = ValidationType.Schema;
      XmlSchemaSet schemas = new XmlSchemaSet();
      settings.Schemas = schemas;
      schemas.Add(test_schema);
      settings.ValidationEventHandler += ValidEventHandler;
      XmlReader validator = XmlReader.Create(config_path, settings);

      while(validator.Read() && !_failed);
      validator.Close();

      if(_failed) {
        throw new Exception(_message);
      }

      return true;
    }

    protected void ValidEventHandler(object sender, ValidationEventArgs args) {
      _failed = true;
      _message = args.Message;
    }
  }
}
