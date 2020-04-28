@echo off
;sc delete STARService
c:\Windows\Microsoft.NET\Framework\v4.0.30319\installutil.exe C:\Users\mini\source\repos\PNFService\PNFService\bin\x86\Debug\DATAService.exe
;REGEDIT /S c:\Users\mini\source\repos\PNFService\PNFService\bin\x86\Debug\Setup.reg