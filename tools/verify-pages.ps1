[CmdletBinding()]
param(
    [uri] $Uri = 'https://josem404.github.io/Mis_widgets_feed/'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Uri.Scheme -ne 'https') { throw 'GitHub Pages debe utilizar HTTPS.' }

$response = Invoke-WebRequest -Uri $Uri -MaximumRedirection 5
if ($response.StatusCode -ne 200) {
    throw "La página devolvió HTTP $($response.StatusCode)."
}

if ($response.Content -notmatch '<title>Mis Feed</title>') {
    throw 'La respuesta no contiene la shell esperada de Mis Feed.'
}

$xFrameOptions = $response.Headers['X-Frame-Options']
if ($xFrameOptions) {
    throw "X-Frame-Options impediría la integración: $xFrameOptions"
}

$csp = [string] $response.Headers['Content-Security-Policy']
if ($csp -match 'frame-ancestors\s+[^;]*(?:''none''|''self'')') {
    throw "La CSP HTTP impediría al Widgets Board incrustar el feed: $csp"
}

Write-Host "GitHub Pages correcto: $Uri" -ForegroundColor Green
Write-Host 'HTTP 200, shell identificada y sin cabeceras anti-frame incompatibles.'
