<?php

declare(strict_types=1);

$root = dirname(__DIR__);
$iterator = new RecursiveIteratorIterator(new RecursiveDirectoryIterator(
    $root,
    FilesystemIterator::SKIP_DOTS,
));
$forbiddenDirectories = ['/.git/', '/bin/', '/obj/', '/TestResults/', '/artifacts/', '/.cache/'];
$patterns = [
    '/-----BEGIN ' . '(?:RSA |EC |OPENSSH )?PRIVATE KEY-----/',
    '/\bgh[opusr]_[A-Za-z0-9]{30,}\b/',
    '/\bxox[baprs]-[A-Za-z0-9-]{20,}\b/',
    '/\bsk-[A-Za-z0-9]{20,}\b/',
    '#/(?:Users|home)/[^/\s]+/#',
];

foreach ($iterator as $file) {
    if (!$file->isFile()) {
        continue;
    }
    $path = $file->getPathname();
    $normalized = str_replace('\\', '/', $path);
    foreach ($forbiddenDirectories as $directory) {
        if (str_contains($normalized, $directory)) {
            continue 2;
        }
    }
    $contents = file_get_contents($path);
    if (!is_string($contents)) {
        continue;
    }
    foreach ($patterns as $pattern) {
        if (preg_match($pattern, $contents) === 1) {
            fwrite(STDERR, sprintf("Secret or local path pattern found in %s\n", substr($path, strlen($root) + 1)));
            exit(1);
        }
    }
}
fwrite(STDOUT, "No secret-shaped values or local absolute paths found.\n");
