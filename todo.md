# FORK: PLANNED

- [ ] Change a book's cover afterwards
  - [ ] pick an image file (or take `cover.jpg` / `folder.jpg` from the book's folder)
  - [ ] search the cover online by title and author and choose one
  - [ ] Windows and Android
- [ ] Fill in a book's metadata from the internet (description, genres, series and part, narrator, release date)
  - [ ] the genres become the starting point for tagging (next item)
- [ ] Genres / tags for audiobooks (filter the library, statistics per genre)
- [ ] Windows: show the total number of books in the library
  - [ ] helps to check that a library scan found everything, and to compare collections
- [ ] Windows: easier import of audiobooks made of several files
  - [ ] import a whole folder at once: every subfolder with audio files becomes one book
  - [ ] files in natural name order by default; sorting them by hand only as an option
  - [ ] makes importing a large library easy
- [ ] Windows: better sound quality at higher speeds
  - [ ] the narrator sounds better on Android (ExoPlayer/Sonic) than on Windows (NAudio + SoundTouch)
  - [ ] analyse the whole chain (decoding, sample rate, time stretching and its settings, buffering, output)
  - [ ] research better approaches online (e.g. SoundTouch settings for speech, WSOLA/Sonic, the Windows playback rate) and compare by listening

# BUGS

- [ ] BUG: when headphones disconnect, the app crashes

# IN PROGRESS


# NOTES:

make a card:

```
<StackPanel 
                    CornerRadius="8"
                    Padding="12"
                    BorderThickness="1"
                    BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
                    Background="{ThemeResource CardBackgroundFillColorDefaultBrush}">
```
