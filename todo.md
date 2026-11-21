# FORK: PLANNED

- [ ] Change a book's cover afterwards
  - [x] pick an image file (or take `cover.jpg` / `folder.jpg` from the book's folder) (Windows: "Change cover…")
  - [x] search the cover online by title and author and choose one (Windows: "Look up online…")
  - [x] Windows and Android (Android: "Change cover" in the book's menu picks an image; no folder cover and no online search there yet)
- [x] Windows: fill in a book's metadata from the internet (description, genres, series and part, narrator, release date)
  - [x] the genres become the starting point for tagging (next item)
  - [x] also change the book's title (never ticked by default)
- [x] Windows: genres / tags for audiobooks (own tags, bulk edit, filter the library, statistics per tag)
- [x] Windows: show the total number of books in the library
  - [x] helps to check that a library scan found everything, and to compare collections
- [x] Windows: easier import of audiobooks made of several files
  - [x] import a whole folder at once: every subfolder with audio files becomes one book
  - [x] files in natural name order by default; sorting them by hand only as an option
  - [x] makes importing a large library easy
- [ ] Windows: better sound quality at higher speeds
  - [ ] the narrator sounds better on Android (ExoPlayer/Sonic) than on Windows (NAudio + SoundTouch)
  - [x] analyse the whole chain (decoding, sample rate, time stretching and its settings, buffering, output): decoding (Media Foundation) and output (MediaPlayer, 16-bit PCM at the file's rate) are lossless; the difference is the time stretching
  - [x] Sonic (the speech method ExoPlayer uses) ported to C# and now the default; Settings → Player → Speed processing switches between Sonic and SoundTouch while a book plays
  - [ ] compare by listening; if Sonic is better, the todo can be closed
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
  - [x] Windows: a file that played and then fails is reopened paused at its position instead of closing the book; media failures and crashes on background threads are now logged with details
  - [ ] test by unplugging headphones; if it still crashes, send Audibly.log (LocalState)

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
