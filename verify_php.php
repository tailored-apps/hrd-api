<?php
// Include exceptions first
require_once __DIR__ . '/src/Exceptions/HRDApiException.php';
require_once __DIR__ . '/src/Exceptions/HRDApiCommunicationException.php';
require_once __DIR__ . '/src/Exceptions/HRDApiIncorrectDataException.php';
require_once __DIR__ . '/src/Exceptions/HRDApiIncorrectDataReceivedException.php';
require_once __DIR__ . '/src/HRDApi.php';

use HRDBase\Api\HRDApi;

// Silence deprecation warnings
error_reporting(E_ALL & ~E_DEPRECATED);


$secrets = json_decode(file_get_contents(__DIR__ . '/secrets.json'), true);
if (!$secrets) {
    die("Could not read secrets.json\n");
}

$login = $secrets['login'];
$password = $secrets['password'];
$apiKeyHex = $secrets['apiKeyHex'];

try {
    echo "Initializing TestHRDApi...\n";
    $apiInstance = HRDApi::getInstance([
        'apiHash' => $apiKeyHex,
        'apiLogin' => $login,
        'apiPass' => $password,
        'debug' => true,
    ]);

    $token = $apiInstance->getToken();
    echo "Attempting Login...\n";
    echo "Login method finished.\n";
    echo "Token: " . $token . "\n";

    echo "Checking Partner Balance...\n";
    $balance = $apiInstance->partnerGetBalance();
    print_r($balance);

} catch (Exception $e) {
    echo "Error: " . $e->getMessage() . "\n";
    echo "Trace: " . $e->getTraceAsString() . "\n";
}
