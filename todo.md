# FORK: PLANNED

- [ ] Change a book's cover afterwards
  - [ ] pick an image file (or take `cover.jpg` / `folder.jpg` from the book's folder)
  - [x] search the cover online by title and author and choose one (Windows: "Look up online…")
  - [ ] Windows and Android
- [x] Windows: fill in a book's metadata from the internet (description, genres, series and part, narrator, release date)
  - [x] the genres become the starting point for tagging (next item)
  - [x] also change the book's title (never ticked by default)
- [x] Windows: genres / tags for audiobooks (own tags, bulk edit, filter the library, statistics per tag)
- [x] Windows: show the total number of books in the library
  - [x] helps to check that a library scan found everything, and to compare collections
- [ ] Windows: easier import of audiobooks made of several files
  - [ ] import a whole folder at once: every subfolder with audio files becomes one book
  - [ ] files in natural name order by default; sorting them by hand only as an option
  - [ ] makes importing a large library easy
- [ ] Windows: better sound quality at higher speeds
  - [ ] the narrator sounds better on Android (ExoPlayer/Sonic) than on Windows (NAudio + SoundTouch)
  - [ ] analyse the whole chain (decoding, sample rate, time stretching and its settings, buffering, output)
  - [ ] research better approaches online (e.g. SoundTouch settings for speech, WSOLA/Sonic, the Windows playback rate) and compare by listening
- [x] Windows: swap the buttons in the "Continue where you left off?" dialog
  - [x] the big blue (primary) button should be "Continue there", the plain one "Stay here"

- [ ] Make the fork usable for others
  - [ ] choose an own app name (the Store name "Audibly" belongs to the original listing)
  - [ ] GitHub release: Windows MSIX with its own identity (installs next to the Store app), signed with an own certificate, plus install instructions for the one-time certificate step
  - [ ] GitHub release: Android APK signed with an own release key (keep the key safe; switching from the debug key needs one reinstall)
  - [ ] later: own Microsoft Store listing (individual developer accounts are free; the Store signs the package)
  - [ ] keep the pull requests to rstewa/Audibly going

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
