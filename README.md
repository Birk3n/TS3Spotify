# TS3SpotifyPlugin

## Features
* Only works with Spotify-Premium!
* Can play Music from Spotify ~~Windows~~ / Linux

## Commands ##
* !spotify play: Starts the Spotify-Device
* !spotify stop: Stops the Spotify-Device


## Checkout
* git clone --recurse-submodules https://github.com/Birk3n/TS3Spotify.git
* Get and build librespot (https://github.com/librespot-org/librespot)
* Add to Bot-Config (bot.toml)
```
 [spotify]
 librespotPath = "/absolut/path/to/librespot"
```
* `dotnet build --framework netcoreapp3.1 --configuration Release TS3SpotifyPlugin`
* copy `.\TS3SpotifyPlugin\bin\Release\netcoreapp3.1\plugins` to your bot
* !plugin load {id}

* !spotify account add (if not already) => write mail + password to **TS3AudioBot private chat** (beware! Password is in plaintext in logs!)

## Stuff
```
System.IO.FileNotFoundException: Could not load file or assembly 'TS3AudioBot, Version=0.12.3.0, Culture=neutral, PublicKeyToken=null'. The system cannot find the file specified.
```
Make sure to use the right version of TS3AudioBot. The Buildversion of plugin and bot must match!

```
Librespot error.
```
Something is wrong with librespot. If are already logged in you can start librespot from cli:
`mkfifo /tmp/TS3AudioBotFifo
/path/librespot -b 160 --enable-volume-normalisation --initial-volume 100 --backend pipe -n TS3AudioBot -c "/opt/ts3bot/bot/plugins/lib/**YOUR_SPOTIFY_ID**" --device /tmp/TS3AudioBotFifo`
it should say something to you.