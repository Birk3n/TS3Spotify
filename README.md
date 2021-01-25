# TS3SpotifyPlugin

## Features
* Only works with Spotify-Premium!
* Can play Music from Spotify Windows / Linux

## Commands ##
* !spotify play < id >: Start the Playlist / Album
* !spotify next (steps): Next song
* !spotify shuffle: show shuffle status
* !spotify shuffle < on|off >: toggle shuffle
* !spotify search < "param" >: Search 


## Checkout
* git clone --recurse-submodules https://github.com/Birk3n/TS3Spotify.git
* Get and build librespot (https://github.com/librespot-org/librespot)
* Add to your Bot-Config 
```
 [spotify]
 deviceName = "TS3AudioBot"
 pipelineName = "TS3AudioBotFifo"
 librespotPath = "../../../../../../path/to/librespot.exe" #starts where your ts3audiobot is
 clientId = "your-client"
 clientSecret = "your-secret"
 returnUrl = "http://your-developer-website.com"
```

## Spotify Client ID ##
* Login in https://developer.spotify.com/dashboard/
* Create a client id
* Fill Name, Description and other info. 
* Get the Client-ID and Secret-ID from your Spotify-Application

## How to use ##
* Type !spotify account
* Go to privat chat and type !spotify account add
* Keep the stuff in privat chat!
* Use the Link, login to spotify and get the auth-token from the get-param
* Paste it into chat
* Write your mail in privat chat!
* Write your password in privat chat!
* Have fun!
