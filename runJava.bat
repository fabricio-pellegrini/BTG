@echo off
echo Checking Java installation...
java -version
if %errorlevel% neq 0 (
    echo Java is not installed or not added to PATH.
    exit /b 1
)
cd java
echo Compiling Java files...
javac *.java
echo Running FileProcessor...
java  FileProcessor
echo Cleaning up...
del *.class 
cd ..
echo Done.