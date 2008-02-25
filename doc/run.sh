ndoc-console ../lib/Ipop.dll,IpopNode.xml -namespacesummaries=namespaces/namespace -documenter=MSDN_2003
monodocer -pretty -importslashdoc:IpopNode.xml -path:xml -assembly:../lib/Ipop.dll -name:IPOP
cp namespaces/* xml/.
monodocs2html --source xml --dest html
