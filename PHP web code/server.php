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

function InitFile( $sFile, $cFile, $toWrite ) //[room#]server.txt
{
	//echo nl2br($toWrite);
	//First check, create, empty #server.txt
	$noError = True;
	$lockFile = getLockFile($sFile); //create lock file name and test for existence

	$myFile = fopen($sFile, "w"); //else create it
	if (!$myFile) //was there an error creating the file?
	{
		$noError = False;//report the error
		//exit(-1);
	}
	else //File succesfully created, add command
	{
		$toWrite .= PHP_EOL;
		if(!fwrite($myFile, $toWrite)) //write out new PPN
		{
			$noError = False; //There was an error
		}
	}
	fclose($myFile);
	unlockIt($lockFile);

	//Now check, create, empty #client.txt
	$lockFile = getLockFile($cFile); //create lock file name and test for existence

	$myFile = fopen($cFile, "w"); //else create it
	if (!$myFile) //was there an error creating the file?
	{
		$noError = False;//report the error
		//exit(-1);
	}
	//If file succesfully created, leave $noError alone
	fclose($myFile);
	unlockIt($lockFile);

	return $noError; //return success or fail
}

function CheckClient( $theFile ) //Reads the <room#>client.txt file and returns the first line is present
{
	//The client can create the following commands or texts in the client file:
	//CMD,<the command>
	//TXT,<text type>,<text>
	//$theFile = "./" . $theRoom . "client.txt";
	$lockFile = getLockFile($theFile); //Lock the client file
	$clientCMD = "";
	$myFile = fopen($theFile, "r");//open it for read
	if(!$myFile) //Error opening the file - abort
	{
		$clientCMD = "ERROR";
		fclose($myFile);
	}
	else
	{
		//$contents = fread($myFile, filesize($theFile)); //read whole file
		fclose($myFile); //close it
		$contents = file($theFile);

		//$contents = file_get_contents($theFile); //read whole file
		if(strlen($contents[0]) > 0)  //Contents found, file not empty
		{
			//$clientCMD = $contents;
			//$new_contents = "";
			//$contents_array = explode(PHP_EOL, $contents); //Break the content in lines
			$new_contents = array();
			$line1 = True;
			//foreach ($contents_array as &$thisLine) //Scan through lines
			foreach ($contents as $thisLine)
			{
				if($line1) //We're only interested in the first line
				{
					$line1 = False; //Only once
					$clientCMD = $thisLine; //Remember the line
					//continue; //skip this line in the new version of the file
				}
				else
				{
					//$new_contents .= $thisLine . PHP_EOL;// . "\r"; //Add each of all the other lines
					$new_contents[] = $thisLine;
				}
			}
			//file_put_contents($theFile, $new_contents); //Write out all lines except line 1
			$myFile = fopen($theFile, "w"); //Recreate the file - empty
			foreach($new_contents as $theLine)
			{
				fwrite($myFile, $theLine);
			}
			//fwrite($myFile, $new_contents);
			fclose($myFile);
		}
		else
		{
			$clientCMD = "EMPTY";
		}
	}
	unlockIt($lockFile);
	return trim($clientCMD);
}

function WriteServer( $theFile, $toWrite )
{
	$noError = True;
	$lockFile = getLockFile($theFile); //create lock file name and test for existence

	$myFile = fopen($theFile, "a"); //open the file for append
	if (!$myFile) //was there an error creating the file?
	{
		$noError = False;//report the error
		exit(-1);
	}
	else //File succesfully created, add command
	{
		$toWrite .= PHP_EOL;
		if(!fwrite($myFile, $toWrite)) //write out new PPN
		{
			$noError = False; //There was an error
		}
	}
	fclose($myFile);
	unlockIt($lockFile);
	return $noError;
}

//Start main code section
$input = $_GET["cmd"]; //Get the Command
//echo nl2br("The data: " . $cmd . "\n");
$elements = explode(",", $input); //Split: room# - command - message
$room = $elements[0]; //[int]
$cmd = $elements[1]; //CMD,TXT
$messsage = $elements[2]; //INIT,POLL,START,NEG,POS,GEN > if GEN then elements[3] holds which #
//elements[3] = PollingInterval only at INIT
//elements[4] = Button TimeOut only at INIT
//echo nl2br ("Parts: " . $elements[0] . "-" . $elements[1] . "-" . $elements[2] . "-" . $elements[3] . "-" . $elements[4] . "-" . $elements[5] . "-" . $elements[6] . "\n");
//Check for commands from server
$serverFile = "./" . $room . "server.txt";
$clientFile = "./" . $room . "client.txt";
$result = False;
if($cmd == "CMD")
{
	//echo nl2br("Command found \n");
	//POLL (look for client CMD), INIT (new session), START (duh), NEG/POS/GEN (button pressed)
	switch($messsage)
	{
		case "INIT": //<room#>,CMD,INIT - New session, create empty server file
			//echo nl2br("Init found \n");
			if(InitFile($serverFile, $clientFile, "CMD,INIT," . $elements[3] . "," . $elements[4] . "," . $elements[5] . "," . $elements[6])) //CMD,INIT,<polling interval>,<button timeout>,<question timeout>,<scenarios>
			{
				echo nl2br("CMD,INIT,OK");
			}
			else
			{
				echo nl2br("CMD,INIT,ERROR");
			}
			break;
		case "POLL": //<room#>,CMD,POLL - See if there's a command or info from the client
			$result = CheckClient($clientFile); //Either "ERROR", or "EMPTY" or the client's command/text
			echo nl2br("CMD,POLL," . $result);
		break;
		case "START":
			if(WriteServer($serverFile, "CMD,START"))
			{
				echo nl2br("CMD,START,OK");
			}
			else
			{
				echo nl2br("CMD,START,ERROR");
			}
			break;
		case "NEG":
			if(WriteServer($serverFile, "CMD,NEG"))
			{
				echo nl2br("CMD,NEG,OK");
			}
			else
			{
				echo nl2br("CMD,NEG,ERROR");
			}
			break;
		case "POS":
			if(WriteServer($serverFile, "CMD,POS"))
			{
				echo nl2br("CMD,POS,OK");
			}
			else
			{
				echo nl2br("CMD,POS,ERROR");
			}
			break;
		case "GEN":
			if(WriteServer($serverFile, "CMD,GEN," . $elements[3]))
			{
				echo nl2br("CMD,GEN,OK");
			}
			else
			{
				echo nl2br("CMD,GEN,ERROR");
			}
			break;
		case "QUIT":
			unlink($serverFile);
			unlink($clientFile);
			unlockIt("./" . $room . "server.lck");
			unlockIt("./" . $room . "client.lck");
			break;
		default:
			echo nl2br("CMD,ERROR");
			break;
	}
}
?>