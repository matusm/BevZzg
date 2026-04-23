TestZzg - NMM Data to BCR Converter
===================================

A standalone command line tool to monitor the time signal generators of the [BEV](https://www.bev.gv.at). These time signal generators (ZZG, "Zeitzeichengeber" in German) are used to distribute time signals over the telephone network.

Two ZZG instruments are operated for redundancy. This app monitores the proper operation of the instruments by some plausibility checks.


## Command Line Usage:

```
TestZzg
```

## Settings:

All parameters are stored in the app's config file. The most important settings are the two COM ports of the instruments, as well as the location of the log files. Also the poling interval (in minutes) can be set.

## Installation
If you do not want to build the application from the source code you can use the released binaries. Just copy the .exe, .exe.cofig and the .dll files to a directory of your choice. This direcory should be included in the user's PATH variable.

## Dependencies and Acknowledgments
* [At.Matus.UI.ConsoleUI](https://github.com/matusm/At.Matus.UI.ConsoleUI)  

---

**Note**: This app and library is not officially affiliated with or endorsed by BEV.
