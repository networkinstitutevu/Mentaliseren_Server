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
			//header("Location: http://www.techlabs.nl/experiments/error.html");
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

function CheckServer( $theFile, $lookForStr ) //[room#]server.txt
{
	$lockFile = getLockFile($theFile); //create lock file name and test for existence
	//$stringFound = False; //Preset set to NOT found
	$values = "";
	$myFile = fopen($theFile, "r");//open the file
	if(!$myFile) //If it's not there
	{
		fclose($myFile); //Close it to make sure
	}
	else //File exists
	{
		fclose($myFile); //Close it
		$contents = file($theFile); //Get content of file
		if(strlen($contents[0]) > 0)  //If content then process
		{
			$new_contents = array();
			foreach ($contents as $thisLine)
			{
				if(strpos($thisLine, $lookForStr) !== false) //Does this line contain the looked for string?
				{
					$values = $thisLine;
					//$stringFound = True;
				}
				else
				{
					$new_contents[] = $thisLine;
				}
			}
			$myFile = fopen($theFile, "w");
			foreach ($new_contents as $theLine)
			{
				fwrite($myFile, $theLine);
			}
			fclose($myFile);
		}
	}
	unlockIt($lockFile);
	return trim($values);
	//return $stringFound;
}

function PollServer( $theFile ) //[room#]server.txt
{
	$lockFile = getLockFile($theFile); //create lock file name and test for existence
	$serverCMD = "";
	$myFile = fopen($theFile, "r");//open the file
	if(!$myFile) //If it's not there
	{
		$serverCMD = "ERROR"; 
		fclose($myFile); //Close it to make sure
	}
	else //File exists
	{
		fclose($myFile); //Close it
		$contents = file($theFile); //Get content of file
		if(strlen($contents[0]) > 0)  //If content then process
		{
			$new_contents = array();
			$line1 = True;
			foreach ($contents as $thisLine)
			{
				if($line1)
				{
					$line1 = False;
					$serverCMD = $thisLine;
				}
				else
				{
					$new_contents[] = $thisLine;
				}
			}
			$myFile = fopen($theFile, "w");
			foreach ($new_contents as $thisLine)
			{
				fwrite($myFile, $thisLine);
			}
			fclose($myFile);
		}
		else //No contents
		{
			$serverCMD = "EMPTY";
		}
	}
	unlockIt($lockFile);
	return trim($serverCMD);
}

function WriteClient( $theFile, $toWrite)
{
	$noError = True;
	$lockFile = getLockFile($theFile); //create lock file name and test for existence

	$myFile = fopen($theFile, "a"); //open the file for append
	if (!$myFile) //was there an error creating the file?
	{
		$noError = False;//report the error
		//exit(-1);
	}
	else //File succesfully created, add command
	{
		$toWrite .= PHP_EOL;
		if(!fwrite($myFile, $toWrite))
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
//echo nl2br ("Parts: " . $elements[0] . "-" . $elements[1] . "-" . $elements[2] . "\n");
//Check for commands from server
$serverFile = "./" . $room . "server.txt";
$clientFile = "./" . $room . "client.txt";
$result = False;
switch($elements[1]) //CMD or TXT
{
	case "CMD":
		switch($elements[2])
		{
			case "POLL":
				$result = PollServer($serverFile);
				echo nl2br("CMD,POLL," . $result);
				break;
			case "INIT": //Check if server is there
				$result = CheckServer($serverFile, "CMD,INIT");
				if(strlen($result) > 0) //If server is ready (file exists and contained INIT)
				{
					$components = explode(",", $result); //[2] = polling interval, [3] = button timeout
					echo nl2br("CMD,INIT,OK," . $components[2] . "," . $components[3] . "," . $components[4] . "," . $components[5]);
					//<polling interval>,<button timeout>,<question mark timeout>,<scenarios>
				}
				else
				{
					echo nl2br("CMD,INIT,ERROR");
				}
				break;
			case "READY": //Client says it's ready to start
				if(WriteClient($clientFile, "CMD,READY"))
				{
					echo nl2br("CMD,READY,OK");
				}
				else
				{
					echo nl2br("CMD,READY,ERROR");
				}
				break;
			case "RPS": //Tell server to ask for button
				if(WriteClient($clientFile, "CMD,RPS"))
				{
					echo nl2br("CMD,RPS,OK");
				}
				else
				{
					echo nl2br("CMD,RPS,ERROR");
				}
				break;
			case "NEW": //Tell server a new scenario starts
				if(WriteClient($clientFile, "CMD,NEW"))
				{
					echo nl2br("CMD,NEW,OK");
				}
				else
				{
					echo nl2br("CMD,NEW,ERROR");
				}
				break;
			case "END": //Tell server end of session = quit
				if(WriteClient($clientFile, "CMD,END"))
				{
					echo nl2br("CMD,END,OK");
				}
				else
				{
					echo nl2br("CMD,END,ERROR");
				}
				break;
			case "ABORT": //Tell server to froce quit
				if(WriteClient($clientFile, "CMD,ABORT"))
				{
					echo nl2br("CMD,ABORT,OK");
				}
				else
				{
					echo nl2br("CMD,ABORT,ERROR");
				}
				break;
			default:
				echo nl2br("Error in CMD");
				break;
		}
		break;
	case "TXT":
		switch($elements[2])
		{
			case "VO": //New Voice Over text. [3] = text and can be empty!
				if(WriteClient($clientFile, "TXT,VO," . $elements[3]))
				{
					echo nl2br("TXT,VO,OK");
				}
				else
				{
					echo nl2br("TXT,VO,ERROR");
				}
			break;
			case "SC": //New scenario starts. [3] = theme name
				if(WriteClient($clientFile, "TXT,SC," . $elements[3]))
				{
					echo nl2br("TXT,SC,OK");
				}
				else
				{
					echo nl2br("TXT,SC,ERROR");
				}
			break;
			case "STP": //Next step in scenario. [3] = [int]
				if(WriteClient($clientFile, "TXT,STP," . $elements[3]))
				{
					echo nl2br("TXT,STP,OK");
				}
				else
				{
					echo nl2br("TXT,STP,ERROR");
				}
			break;
			case "LARS": //Text spoken by Lars. [3] =<spoken text> can be empty
				if(WriteClient($clientFile, "TXT,LARS," . $elements[3]))
				{
					echo nl2br("TXT,LARS,OK");
				}
				else
				{
					echo nl2br("TXT,LARS,ERROR");
				}
			break;
		}
	break;
}
?>