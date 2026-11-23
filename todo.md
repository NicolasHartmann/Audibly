# FORK: PLANNED

- [x] Change a book's cover afterwards
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
- [x] Windows: better sound quality at higher speeds
  - [x] the narrator sounds better on Android (ExoPlayer/Sonic) than on Windows (NAudio + SoundTouch)
  - [x] analyse the whole chain (decoding, sample rate, time stretching and its settings, buffering, output): decoding (Media Foundation) and output (MediaPlayer, 16-bit PCM at the file's rate) are lossless; the difference is the time stretching
  - [x] Sonic (the speech method ExoPlayer uses) ported to C# and now the default; Settings → Player → Speed processing switches between Sonic and SoundTouch while a book plays
  - [x] compared by listening (2026-11-21): Sonic is clearly better, especially at the ends of words
- [x] Windows: swap the buttons in the "Continue where you left off?" dialog
  - [x] the big blue (primary) button should be "Continue there", the plain one "Stay here"
- [x] Windows: import several audiobook folders at once
  - [x] pick several folders in one go; each becomes one book (or, for a library folder, each subfolder with audio files)
  - [x] a one-time import, not a watched library folder
  - [ ] test in the app (dialog, two folders, folder + its subfolder)
- [ ] Windows: a second progress display in the player
  - [ ] when the progress of the whole book is shown, the progress of the chapter can be shown as well, and the other way round
  - [ ] the second display has the same three modes: elapsed/total, time remaining, percent
- [ ] Windows: ebook reader (read-along): chapter selection and progress
  - [ ] pick a chapter in the reader and jump there
  - [ ] show progress of the whole book and of the current chapter
- [x] Windows: PayPal link in Settings points to the fork author instead of rstewa (Audibly.App/Views/SettingsPage.xaml.cs, `paypal.me/rstewa35`)

- [ ] Make the fork usable for others
  - [x] choose an own app name: "Audibly Fork" for now (package NicolasHartmann.AudiblyFork)
  - [x] GitHub release: Windows MSIX with its own identity (installs next to the Store app), signed with an own certificate, plus install instructions for the one-time certificate step
    - [x] own self-signed certificate created (CN=NicolasHartmann, valid until 2036; kept outside the repo)
    - [x] signed MSIX built with tools/build-release.ps1; install steps in the README
    - [x] upload as a GitHub release
    - [x] crash reports: off (the Sentry DSN is empty in the repo, so fork builds send nothing; user chose to keep it off)
  - [x] GitHub release: Android APK signed with an own release key (keep the key safe; switching from the debug key needs one reinstall)
    - [x] release key created and signed APK built
    - [x] upload as a GitHub release
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
