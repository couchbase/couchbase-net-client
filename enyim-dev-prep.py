#Because the submodule Enyim.Caching is setup to be delay signed when the private key isn't 
#present, you'll need to modify the project or disable assembly verification
#to be able to use a local build.  The simplest solution is to disable delay signing
#after updating your submodule.  This IronPyton script is a QUICK and DIRTY solution to do that.
#Again, this script is only useful for local development when working with the source

from System.IO import StreamReader, StreamWriter
from System.Text import StringBuilder

path = r"lib/EnyimMemcached/build/CommonProperties.targets"

sr = StreamReader(path)
sb = StringBuilder()

line = sr.ReadLine()
while not line is None:
    
    #I'm sure this could all be done in a couple of lines with a nice multi-line Regex
    #All this does is comment out property groups that attempt to set signing 
    if line.Trim().StartsWith("<PropertyGroup") and line.Contains("PrivateKey"):
        
        sb.AppendFormat("<!--{0}\r\n", line)                 
        while line is not None and line.Trim() != "</PropertyGroup>":
            sb.AppendLine(line)
            line = sr.ReadLine()
        else:
            sb.AppendFormat("{0}-->\r\n", line)            
    else:
        sb.AppendLine(line)
    
    line = sr.ReadLine()
        
content = sb.ToString()
content = content.Replace("<DelaySign>true</DelaySign>", "<DelaySign>false</DelaySign>")
content = content.Replace("<SignAssembly>true</SignAssembly>", "<SignAssembly>false</SignAssembly>")
sr.Dispose()

sw = StreamWriter(path, False)
sw.Write(sb.ToString())
sw.Dispose()