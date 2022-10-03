<?php
// ********
// This opens the ppn.txt file which holds the latest ppn
// Then it increases the number, writes it out to ppn.txt again
// And does a http request with the new ppn so the Lua script can pick it update
// ********

//Creates lock file name
function getLockFile ( $theFile ) {
	$lockFileName = trim(substr( $theFile, 0, strpos($theFile, "txt")) . "lck");
	if (!file_exists($lockFileName)) { //No lock file yet? Create it.
		$myLockFile = fopen($lockFileName, "w");
		if (!$myLockFile) {
			echo "Lock file could not be created</br>";
			header("Location: http://www.techlabs.nl/experiments/error.html");
			exit(-1);
		}
		fclose($myLockFile);
	}
	return $lockFileName;
}

//Locks the lock file to indicate other processes can not use our file
function lockIt( $theFile ) {
	chmod($theFile, 0400); //Make lock file non-writable
	return;
}

//Unlock the lock file so other processes know they can access our file
function unlockIt ( $theFile ) {
	chmod($theFile, 0644);
	return;
}

$data = $_GET["data"];
$theFile = "./ip.txt";
$lockFile = getLockFile($theFile); //create lock file name and test for existence

//Check if the file exists
if (!file_exists($theFile)) {
	//else create it
	$myFile = fopen($theFile, "w");
	//was there an error creating the file?
	if ( !$myFile) {
		//report the error
		echo "Error creating ip.txt</br>";
		header("Location: https://www.techlabs.nl/experiments/test.html");
		exit(-1);
	}
	fclose($myFile);
}
unlockIt($lockFile);

//Wait for unlocked lock file
while (substr(sprintf('%o', fileperms($lockFile)), -4) == "0400") {
	usleep(rand(10,300));
	clearstatcache();
}
lockIt($lockFile); //Lock it for us
$myFile = fopen($theFile, "r"); //open to read

//$result = ftruncate($myFile, 0); //Empty file
//if ( !$result) {
//	echo "Error emptying ppn.txt</br>";
//	fclose($myFile);
//	unlockIt($lockFile);
//	header("Location: http://www.networkinstitute.org/research/walkingipads/error.html");
//	exit(-1);
//}
if ( !$myFile) {
	echo "Error opening ip.txt</br>";
	unlockIt($lockFile);
	header("Location: https://www.techlabs.nl/experiments/test.html");
	exit(-1);
}

$line = fread($myFile, 1024); //Read line, 1024 bytes is enough, read until EOF
fclose($myFile); //Close the file
unlockIt($lockFile); //unlock it

echo $line;

//header("Location: http://www.networkinstitute.org/experiments/walkingipads/ppn.html?$newPPN");
//echo "ppn:" + $newPPN;
//echo '<?xml version="1.0" encoding="ISO-8859-1" ? >';
//echo '<rss version="2.0">';
////echo '<channel>';
//echo '<ppn>';
//echo '<title>' + $toWrite + '</title>';
//echo '</ppn>';
////echo '</channel>';
//echo '</rss>';
?>