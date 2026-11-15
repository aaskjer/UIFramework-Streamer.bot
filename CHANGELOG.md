# Changelog

The release workflow puts the section for the tagged version into the GitHub release, so every version needs
its section here before the tag is pushed. Everything before 1.0.0 was published as a pre-release. Since 2026-11-08
only a round version (2.0.0, 3.0.0) is a stable release; every other one is published as a pre-release, and
`lib/v1/latest.json`, which the games' settings actions read, names the newest 1.x of either kind; 2.0.0 and what
follows it go to `lib/v2/latest.json`.

## [2.0.0] - 2026-11-15

The window comes in French and Brazilian Portuguese, a path field opens Windows' file dialog, and a group field
suggests Streamer.bot's groups. 2.0.0 starts the v2 line: its `latest.json` is `lib/v2/latest.json`, which settings
action 0.3.2 reads. A project gets 2.0.0 offered once it ships stub 0.3.2 and its host block; one on an older stub
reads `lib/v1/latest.json`, stays on 1.9.0 and uses 2.0.0 only when another project has installed it. Nothing was
removed: the window still works with every settings action from stub 0.1.0 on (`MinHostStub` stays 0.1.0).

### Added

- **French and Brazilian Portuguese.** `fr.json` and `pt-BR.json` carry every page, key, placeholder, text and
  window text, translated from the revised English and read a second time; no native speaker has read them yet. The
  language picker takes the width of its longest name now, at least the 110 px it had and at most 220, so "Português
  (Brasil)" is no longer cut off. A test holds every language file the DLL carries to English's entries and
  placeholders.
- **"Fix a translation", an issue form for the window's texts in any language.** It asks for the language, what the
  window says now and what it should say. It links the new section *Proofreading a language* in
  [docs/Conventions.md](docs/Conventions.md#proofreading-a-language), which shows how to try a wording first in a
  `UIFramework\lang\<code>.json` beside Streamer.bot.
- **A `path` field has a button for Windows' file or folder dialog.** [docs/Schema.md](docs/Schema.md) promised "a
  folder/file dialog", but the field was a text box with a button that only opened the folder in Explorer. The new
  button opens a file dialog when the value or the default names a file (it has an extension, or a file of that name
  exists), otherwise a folder dialog. The dialog starts where the value points, or where the default points while
  the value is empty; a relative value counts from the Streamer.bot folder. A file the action has not written yet
  can be chosen. A choice under the Streamer.bot folder is written relative to it (`overlay\state.json`), anything
  else as a full path.
- **A `grouplist` field suggests Streamer.bot's groups (stub 0.3.2).** docs/Schema.md promised "chips with
  Streamer.bot's groups suggested"; the field had the hint "Add a Streamer.bot group" and took what was typed. The
  first time a group field gets the cursor, the window asks the settings action for the groups, once. The action
  reads them with `CPH.GetGroups()` on Streamer.bot's thread, and the window stays usable meanwhile. A list under
  the box then shows the groups not added yet, narrowed by what is typed: Up and Down pick one, Enter or a click
  adds it, Esc closes the list. Enter without a picked group adds what was typed, as before. The list comes with the
  DLL, the groups with stub 0.3.2; with an older settings action, and in the preview, the field works as it did.
- **A request that takes long on purpose says it is still at work: `UiHost.StillWorking()` (host block, stub
  0.3.2).** The project calls it while such a request runs (a Try button that imports a word list), and the settings
  action waits 30 seconds from then for the answer. It gave every request 30 seconds from its start and then dropped
  the answer while the work went on; Hangman stopped its import at 28 seconds for that. Without the call, a request
  still gets 30 seconds. A request from a window opened again meanwhile waits for one that says it still works
  instead of starting beside it after 35 seconds, and a request that hung before it no longer takes its
  `StillWorking()` away when it ends. See *The game's side* in [docs/Schema.md](docs/Schema.md).

### Changed

- **The update dialog says when Streamer.bot needs a restart (stub 0.3.1).** A newer UIFramework can be used at once
  only while none runs in this Streamer.bot; a project that loads it at the start (the update notice, `UiLive`) or a
  window opened before keeps the running one in use. The dialog now says which case it is: "Streamer.bot needs no
  restart", or "takes effect once Streamer.bot is restarted" with the version that runs until then. After a download
  that has to wait, a second note says it once more (restart Streamer.bot; no hurry, it can wait until after the
  stream). Before, the window opened as usual and only the log told. Nothing changes in the DLL; a project gets it
  when it ships stub 0.3.1 or later.
- **A language code means its language in any case, and across a region.** The layout file's `language`, a project's
  fixed language (`InLanguage`), the codes of its `Lang` and the names of the files in `UIFramework\lang` were
  matched letter for letter. `"language": "DE"` or `"de-AT"` opened the window in English, and a project's
  `Lang("pt-br", …)` beside the DLL's `pt-BR`, or a `pt-br.json` beside Streamer.bot, added a second Portuguese to
  the picker whose words never reached the framework's. Now a code matches a language in any case. A regional code
  falls back to its language (`de-AT` is German), a language to its only regional file (`pt` is `pt-BR`), and
  anything else to English. The picker, the layout file and the update notice keep the language file's spelling.
- **An `en.json` in `UIFramework\lang` corrects English.** A file there was laid over every language the DLL carries
  except English, which always came from the DLL, so a proofreader's corrections to English never showed. It is now
  read when the window opens, as `de.json` there is. Without such a file, English is the DLL's own, loaded once.
- **The window's texts in English, German and Spanish are read again.** Stiff wording is plainer now, and a text
  that meant whatever the window belongs to, rather than a game, says "extension". Three said what the window does
  not do and are corrected: the accent colour holds for this extension's window only, *Reset layout* also takes the
  help under the fields back into the tooltips, and a newer version shows as a red label at the top of the window,
  not on the About page.
- **The Help page comes by itself** when the window declares help sections, custom triggers or methods and does not
  name `help` among its pages, as Messages and About already did. The triggers with their Fire buttons and the
  method list had no page.
- **The red "i" names a log declared without its page, and a range slider whose whole default meets a fraction.** A
  window with `Log(folder, files)` that does not name `log` had no Log page and no switch for the log, without a
  word. A range slider's default decides what it holds ([docs/Schema.md](docs/Schema.md)), so `S("x", 1, kind:
  "slider", min: 0, max: 2, step: 0.5)` is a slider of whole numbers on a 0.5 grid: it saves 0.5 as 0 and 1.5 as 2,
  the same as the place next to each, and nothing said so. The rule stays; the check names a slider whose default is
  a whole number while its step, minimum or maximum has a fraction, and says to write the default as `1.0` for a
  slider of decimals.
- **An action's `status` reaches the footer also when the action has a `panel`.** With a panel, and so with every
  `auto` action, the status went nowhere, or into the panel only while the panel was empty, and the documented
  answer with `lines`, `bar`, `set` and `status` could not be shown whole. The footer now shows the status beside
  the panel.
- **An `emote` field with `platform: youtube` shows its picture,** as a list's emote column does: YouTube's own
  emojis (`:name:`) from the table inside the DLL. The single field had no picture at all.
- **The docs are read again,** README and docs/ alike, as the code now is and for any project: plainer wording,
  "project" or "extension" where any user of the framework is meant, "game" only where a game is. Corrected where
  text and code disagreed: field help as a tooltip, units in the label, `step` only for sliders, a percent compared
  as stored, `path` and `grouplist` as they now work, the accent colour per extension, `Lock()`, the Log page's
  order, what keeps a 2.x DLL from a 1.x settings action, the troubleshooting rows. README's tree and tool list
  follow the repository. The demo's README counts the settings it declares as they are and names the tick on
  *Precompile on Application Start* that the update notice needs.
- **The demo and the mirrors in `hosts/` carry the host block of stub 0.3.2,** and the demo's import is built again.
- **CI runs once per commit and checks what is made from the sources.** It ran on every push to every branch, and a
  branch with a pull request had each check run twice; it now runs on pushes to `main` and on pull requests. Beside
  the build, the tests and `docs/Conventions.md`, it checks that the demo and the eight games' copies in `hosts/`
  carry the template's host block and that the demo's import carries the demo and the stub.
- **The release workflow runs CI's checks as well.** It does not wait for CI, and 1.6.0 was published while CI
  failed on its `docs/Conventions.md`. A tag is released only when the conventions doc, the demo's host block and
  the demo's import pass there too.
- **Leftovers are gone.** The seven facades in `hosts/` (six games' `SettingsFacade.cs` and Blackjack's
  `StatsFacade.cs`) were copies of the games' settings classes from the port to the generic stub; nothing read them,
  and they had drifted from the games. Three empty `.gitkeep` files are gone with them, and
  `tools/spec-to-window.ps1` leaves too: no project has had a `spec.json` of its own since 1.9.0.

### Fixed

In the DLL, for every project with the next UIFramework release:

- **A save keeps what changed while the project checked it.** A save waits for the project's `validate`. An answer
  that came in meanwhile (a preset button's or an `ask` field's `set`) or a timer put its values into the fields
  after the save had taken its copy, and the window said "Saved" with no unsaved change while the file held the old
  values: closing asked nothing, and the values were gone. They now count as unsaved, the status line says how many,
  *Save & Exit* asks before it closes, and the next save writes them.
- **The questions after the project's check belong to the window.** When the project took its time and the user went
  to another window meanwhile, *Not checked* and *Check your settings* (and the notes after a save or an import that
  failed) had no owner. The settings window took clicks again with the save still open, its X closed it while the
  question stayed up, and the settings could not be opened again until that question was answered. The questions are
  now modal to the settings window.
- **Reset all keeps the unsaved rows of a list in a file of its own.** Such a list is not reset, as the question
  says, but rows typed into it and not yet saved were replaced by the file's without a word, and the status said "No
  unsaved changes". They stay in the list now, still unsaved, as *Reset page* leaves them.
- **The red "i" speaks of an unreadable list file in the window's language.** The line was English in every window.
  It now names the list and its file, followed by the list's own tooltip (`list.unreadable`).
- **A settings file gone beside its `.bak` is shown from the `.bak`, as the project reads it.** When the settings
  file was missing and its `.bak` lay beside it (a swap that was cut off, or a file deleted by hand), the project's
  `UiSettings.Load` ran on the `.bak`, but the window showed the defaults, and its first save, an import or *Reset
  all* wrote them over what the project was using. The window now reads the `.bak` too and says so in the log and in
  a line under the red "i"; its next save writes the settings file from the values it shows. A file that was never
  saved still gives the defaults on both sides.
- **A `null` in the settings file stands for the setting's default, in the window as in the project.** The host
  block's `UiSettings` reads past a `null` to an old key, then to the default. The window took the `null` as the
  value and showed 0, an empty box or a switch that is off, none of which the project went by, and dropped an old
  key's value beside it. It now reads the default (or the old key's value) as the host block does, and the next save
  writes that in place of the `null`. An import that names a setting with `null` brings nothing for it.
- **An old key's factor leaves no float noise in the settings file.** An old fraction moved to a percent with its
  factor of 100, such as 0.29, was written as `28.999999999999996` while the box showed 29 (Hangman's overlay
  opacity, whose default is a decimal). The moved value is now rounded to ten decimals, as a number box saves it.
  The host block rounds it the same way (below).
- **An import takes a slider's value through the slider.** It wrote whatever the file named for a slider: a number
  beyond its range, text where its number belongs, a stop it does not have. The window then showed the slider at its
  end and called the value corrected, while the project read the file's value until the next save. A number is now
  pulled into the range (to a whole number on a whole-number slider); text or a stop the slider lacks is not taken,
  and the dialog says why, as for a dropdown. If the import names the value the slider already holds, one none of
  its stops offers, that value stays.
- **An import trims a token, a path, a colour and an emote as a save does.** A token with a line break after it in
  the imported file was written with it and stayed so until the field was changed. A plain text field still keeps
  the spaces at either end, as typed.
- **A click on the stop a slider sits on chooses it.** A slider over stops that holds a value none of them offers
  names that value beside its label and sits on its first stop. A click on that stop's caption or on the thumb chose
  the stop without the window noticing: the count of unsaved changes stayed the same, a condition on the slider did
  not follow, and a field with `ask` did not ask the project. It now counts as one change.
- **A slider over decimal stops finds the stop of its number.** A default or a `set` of 2.0 arrives as 2, and with
  the stops `"0.5"`, `"2.0"` and `"5.0"` it was taken for the index of the third stop: the slider showed *Fast* and
  the next save wrote `"5.0"`. A number now finds the stop of the same value however either is written (`"0.50"` is
  the stop `"0.5"`); an index finds a stop only among stops that are no numbers.
- **A slider over a range starts on its default when the file holds no number.** With `""` or `"abc"` stored, it sat
  on its minimum, and the next save wrote that minimum. It now does what a number box does: it shows the default,
  and the save writes it.
- **A field with `ask` asks with the values the window shows.** It sent the saved ones. A preset slider that moved
  from `live` to `ask` (the name 1.9.0 gave it) was answered for the stop it was saved on, and `set` put that stop's
  numbers back. On such a field `ask` now means what `live` meant, as docs/Schema.md says. A window of 1.9.0 does it
  with `live` beside `ask`, which the host block of stub 0.3.2 writes by itself (below).
- **An `ask` field's answers land in the order it asked.** An answer that waited behind another request of the
  settings action and came after a later one put its values over the later one's: the slider stood on one stop and
  the fields held another stop's numbers. The field numbers its questions as a button does, and a late answer to an
  earlier one is dropped.
- **An action's box no longer says it failed after an answer came.** When a Try button's ask failed (the project did
  not answer in time) and the next answer only set fields or brought a `status`, the box went on showing the
  failure. That answer takes the failure's place now: its status shows, or the box hides when there is none. The
  lines of an earlier answer still stay under one that only sets fields.
- **An answer under a row hides with its action also with the help under the fields.** With help lines on, a button
  in a named row whose condition hid it left its answer box showing under the row.
- **The Help page's Fire button no longer freezes the window, and it says why the action did not answer.** It asked
  the project's action on the window's own thread: with another request still running, the window froze for five
  seconds, and the settings action's reason for no answer was replaced by the hint that the action may run an older
  version. It now asks the way a Try button has since 1.9.0. The button stays greyed until the answer comes, and no
  answer shows the settings action's reason. An answer that is none at all (a host block that does not know the
  request) keeps the older-version hint.
- **The project's emote sources are asked without holding the window still.** The window asked for them (the
  broadcaster's Twitch id and token, the project's own YouTube pictures) on its own thread. As the first emote field
  was built, that froze the window for up to five seconds while the action was busy; a Try box with YouTube emotes
  (`:name:`), asking before any emote field had, waited up to 35 seconds when the project's handler hung. They are
  now asked once per window on another thread, and each picture shows once the answer has come. A shortcode holds
  its picture's place until then and is drawn as text when it has none. A Unicode emoji still shows at once.
- **The tooltip of a 7TV, BTTV or FFZ emote picture is in the window's language.** It was built in English in code;
  it is now the window text `field.emote.extension`, in every language the DLL carries.
- **A sender switch the Messages page does not draw is a field on its page.** The window kept `useBot`,
  `sendAction`, `numberSeparator` and the two cooldown switches for the Messages page, which draws them only while
  it has messages, and the cooldown pair only while a message is in the cooldown group. A project without messages,
  or one whose cooldown messages have no group (Blackjack's), had settings its action reads and no field for them.
  Such a switch is now a field on its own page (`general`, `cooldowns`, or the page it declares), and a page that
  names one shows it. A standard page with nothing of its own (Messages without messages, Help without help) now
  shows the fields declared for it; before, it was left out together with them.
- **`hidden` hides the sender switches, the number format and the log switch.** The Messages and Log pages drew them
  hidden or not; only `Senders(useBot: null)` left one out. A setting the project hides (`hidden`, `Override(key,
  hidden: true)`) has no field there now, and its value stays in the file.
- **A page's own title stays in a language that does not title its id.** In a language with only the project's own
  texts (`Lang("nl", …)`) or a language file without page titles, the conventions' English title replaced the
  declared one: "How to play" read "Help", "Scoreboard" read "Leaderboard". As with a key's label, the conventions'
  title for the id is taken only where it is a translation.
- **The red "i" looks for split sections on the page a field ends up on.** It read the page each setting declared,
  before the window's pages claimed their fields. So it called a section split that a page's claim had put back
  together, and missed one that a page collecting the fields of two pages split.
- **The timer's random switch sits on the row of its two intervals, and the longest wait greys out with the timer's
  switch too,** as docs/Schema.md says. The random switch stood on a line of its own, and with it on, the longest
  wait stayed open while the timer was off.
- **The About page translates the credits and the project's own links.** The `Tx` translations of the credits under
  the name and of the texts in `About(links:)` were not used, although the page's sections and the links inside them
  were translated and docs/Schema.md said `Tx` translates the credits. They now go through the same lookup as every
  other declared text.
- **Customise mode: *Move up* and *Move down* move a field on the page it is shown on.** The sender switches, the
  number format and the log switch, which the Messages and Log pages draw wherever they are declared, counted among
  a page's fields. On High-Low's Cooldowns page and Blackjack's Messages page a press went past one of them and
  nothing moved, the first field shown could still be moved up, and a field moved next to one took a section the
  page does not show. They are left out now.
- **Customise mode: *Move up* and *Move down* cross a heading one place at a time.** The first field under a
  heading, moved up, landed between the last two fields of the section above; the last field of a section, moved
  down, went past the first field of the next. The places next to a heading could only be reached with the mouse. A
  step over a heading now takes the field into that section and leaves it where it stands.
- **Customise mode: a field moved to another page no longer splits a section there.** *Move to page* and a drop on a
  page in the rail left the field at its declared place among all settings, which on a page the user had sorted fell
  anywhere, often between two fields of one section, whose heading then showed twice. The field now goes to the end
  of its own section when that page has it, else to the end of the page, on a line of its own. The menu takes it out
  of its row too, as the drop did.
- **Customise mode: on a page the user sorted, a field the user never placed goes where the project declares it.**
  Sorting a page gives each of its fields an order in the layout file. A field without one, which the project added
  in an update or *Reset this field* gave back, kept its place among all the project's settings; on a sorted page
  that place could be anywhere, often inside another section, whose heading then showed twice. It now goes behind
  the field the project declares before it on that page when the two share a section, else to the end of its own
  section there, else to the end of the page.
- **An order typed into the layout file with too many digits no longer keeps the window from opening.** A whole
  number longer than 19 digits made every open fail. It now reads as the largest order, as a value of the wrong kind
  reads as not set.
- **The folder button of a `path` field opens the folder a file is in.** For a value that names a file
  (`overlay\state.json`), the button made a folder with the file's name, after which the action could not write its
  file there; for a file that already existed it failed, and only the log said so. It now opens the file's folder,
  and creates that folder when it is missing, as it does for a folder.
- **Log page: the folder button opens the log folder beside Streamer.bot.** It opened the declared folder (`Chat
  Games/Logs`) relative to the process's current directory, which a file dialog moves (the window's own Export and
  Import, any dialog of Streamer.bot). After an export to the Desktop it created and opened an empty `Desktop\Chat
  Games\Logs`. It now opens the folder the page follows and the settings action reads, under the Streamer.bot
  folder.
- **Log page: lines written in the same second show newest first.** The page sorts by time, which has whole seconds,
  and kept lines of equal time in the order it got them. The settings action hands them over as they were written,
  so lines written together, several in one second, showed oldest first inside a list that is newest first. The last
  line written is now the first row, on open and on every reload.
- **Log page: a line without a readable time leaves the Time column empty.** It showed `0001-01-01 00:00:00`. A
  click on the column header still sorts by time.
- **The update notice leaves out a UIFramework release the settings would not offer.** A release skipped with *No*
  in the update dialog, or any release while the release check is off (`skipVersion`, `"autoUpdate": false` in
  `UIFramework\loader.json`), was named in chat at every Streamer.bot start, and the log line sent the streamer to
  settings that did not offer it. The notice now follows the settings action's rules. A project whose own
  `uiframework.layout.json` skips the release or switches the check off counts as not offering it, and UIFramework
  is named while at least one project of the run would offer it. An unreadable `loader.json` counts as empty, as in
  the settings action. The projects' own releases are named as before.
- **A language file with broken braces no longer costs the update notice.** A file in `UIFramework\lang` beside
  Streamer.bot whose *import it again* text had lost a brace made the notice throw while it was put together, and
  that Streamer.bot run had no notice. The text is now filled the way the window fills its own, and a text with
  broken braces is used as it stands.
- **The update notice uses the language a project fixes for its window.** It followed the language in the layout
  file, which a window with `InLanguage("de")` never writes, so that project's notice came in English or in a
  language picked before. The window now tells the notice its fixed language when it opens, and the notice keeps it
  for the Streamer.bot starts that follow.
- **The chat preview groups amounts as the window's language does,** for a project without a thousands-separator
  setting. It wrote 1,500 in English and 1.500 in every other language. Now it takes the separator Windows gives the
  language: 1.500 in German, Spanish and Brazilian Portuguese, 1 500 in French. French officially uses a narrow
  no-break space; the preview writes a plain space, which chat and the text box both show. A language Windows does
  not know keeps the point.

In the settings action, for a project that ships stub 0.3.2:

- **A running UIFramework that needs a newer settings action is not taken (stub 0.3.2).** A host block of 1.9.0 or
  before, in another project, still loads the newest DLL as Streamer.bot starts. The stub passed such a DLL over
  when it was installed but used it when it was running, and the DLL then refused the window with the advice to
  remove its folder. The stub now leaves it alone in both cases; its dialog names the running version and the stub
  it needs, and says that updating the project opens the window.
- **A damaged DLL is set aside also while an older UIFramework runs (stub 0.3.2).** As Streamer.bot starts, the host
  block passes over a newest `UIFramework.dll` that fails its hash and loads the next older one. The settings action
  then used the running one without checking the newest, so the damaged file stayed where it was: its version was
  never offered again, and at every start the log said it would be used from the next one. The settings action now
  checks the hash of the DLL it chose also beside a running UIFramework and sets a damaged file aside as `.broken`;
  the next open offers that version again.
- **The release check's log line calls the installed version installed (stub 0.3.2).** With a project that loads
  UIFramework as Streamer.bot starts, the line called the newest installed version "in use" while an older one ran,
  and when the chosen DLL had failed and been set aside, the log still said it would be used from the next start.
  The line now says "installed". The next-start line comes only when the chosen DLL is still there beside a
  UIFramework that ran before the window opened.
- **The project hears of every save (stub 0.3.2).** The "saved" request runs from the settings action's queue. When
  it found another request still running, the log said "A queued step failed" and the project never heard of the
  save, so its overlay and timed action went on with the old settings. It is now sent again, three tries in all, and
  what still fails is said in the log and in a note beside the window. A run of the project's action that ended
  without answering counts as not heard too.
- **A download that breaks off is tried again (stub 0.3.2).** A connection reset while the file came in was not
  counted as a broken connection, and a connection closed early left a short file that failed the hash check with a
  message that read like tampering. Both get the three tries now, as does a connection that cannot be made; only
  after the last one does the dialog say that the file does not match.
- **A project's action that runs without answering is "no answer" (stub 0.3.2).** One whose `Execute()` never
  reached `UiHost.Handle` counted as an empty answer: a Try button said "done", and a save went on as checked.
  Streamer.bot reads a global set to empty text back as null, so the stub now marks the answer's variable before the
  run and sees whether the project wrote it.
- **A DLL in a folder named `1.9` gets the no-answer marker (stub 0.3.2).** It was compared as older than 1.9.0 and
  got null instead.
- **A list or an object where a version belongs no longer breaks every open (stub 0.3.2).** `skipVersion` in
  `loader.json` or the layout file, or `minHostStub` in `latest.json`, made the window fail. Such a value counts as
  missing now, and the log says so.
- **An unreadable `loader.json` is not written over (stub 0.3.2).** A damaged or locked `UIFramework\loader.json`
  counted as empty, and *No* in the update dialog replaced it with a file holding only `skipVersion`. It now counts
  as empty without being written over, as the layout file already did; *No* keeps the skip in the project's layout
  file then, and the log says where it went (or that it could not be kept) and that `loader.json` cannot be read.
- **A run that a later one took over ends there (stub 0.3.2).** When a project's action that had hung for a minute
  answered after all, its first run went on to the update dialog and the window, beside the run that had opened it.
- **Two runs that end together leave the window free (stub 0.3.2).** If the run that opened the window ended just as
  another run's window closed, neither handed the window back, and the next open reused the old schema without
  asking the project or GitHub.

In the host block, for a project that ships the host block of stub 0.3.2:

- **A DLL that needs a newer settings action no longer locks a project's window (host block).** The update notice
  and `UiLive` loaded the newest `UIFramework.dll` under `lib\`, also one another project had installed for a newer
  stub. Only one UIFramework runs in a Streamer.bot, so the project's settings window then failed on every start,
  although the stub passes such a DLL over. The host block now loads the newest DLL that fits the settings action
  released with it (`StubVersion` in the host block, 0.3.2, kept equal to the stub's); a DLL that names no stub
  fits. The block reads the DLL's version without `System.dll`, so it still compiles in an action that lists only
  mscorlib, System.Core, Newtonsoft.Json and Streamer.bot's interface.
- **The update notice takes the next older DLL when the newest cannot be used (host block).** When the newest
  `UIFramework.dll` under `lib\` failed its hash or threw once while loading, the block gave up on every DLL for the
  rest of the run: no update notice, and `UiLive` said Unknown for Twitch and Kick until Streamer.bot restarted. It
  now tries the installed ones newest first and passes over one that is damaged, will not load, or needs a newer
  settings action (the entry above); a file downloaded again is tried again. Once a UIFramework is loaded, the block
  takes that one: .NET answers every later load of a `UIFramework.dll`, whichever version, with the one already
  loaded.
- **`UiSettings.Load` during a save reads the saved settings (host block).** The window swaps the new settings file
  in, the old one becoming the `.bak`, and for that moment there is no file. `UiSettings.Load` took the missing file
  for one never saved and gave the defaults without a word. It now waits a moment for a file that is being swapped
  in, and reads the `.bak` (saying so in `Problem`) when only that is left. A file missing beside its `.bak` with no
  save under way is read from the `.bak` at once, without the wait; a project whose settings were never saved still
  gets the defaults at once.
- **A save that switches the timer off keeps it off (host block).** `UiTimer.Rearm` looked at the timed action, then
  switched it off and on; a save whose `UiTimer.Sync` switched it off in between found it on again. The two now take
  turns.
- **`UiSettings` reads an old key that is a setting of its own for that setting only (host block).** A setting whose
  `aliases` named another setting's key read that setting's value while its own key was not in the file yet. The
  window leaves such an old key alone (the red "i" names it) and showed the default, so the project ran on another
  setting's value until the first save. It now gets the default, as the window shows.
- **An old value moved with `aliasFactor` is read as the window stores it (host block).** `UiSettings` multiplied it
  as it was, so an old fraction of 0.07 became 7.000000000000001 percent in the project, also in a message that
  prints it. It is now rounded to ten decimals, as the window's store does.
- **A field with `ask` is asked with what the window shows, also in a window of 1.9.0 (host block).** The released
  DLL hands a request the window's current values only for a setting marked `live`, so a field declared with `ask`
  alone (a preset slider) was answered from the values saved last, not from where the slider stood. The host block
  now writes `live` beside `ask` for every field that is no button.

In this repository's tools and workflows:

- **The release workflow refuses an empty changelog section also when the next heading follows at once.** PowerShell
  counts a range down as well: with no line between the version's heading and the next one, both headings were taken
  as the notes, and an empty section was released.
- **`tools/check-stub.ps1` compiles into a folder of its own per checkout.** Two checkouts (worktrees) checked at
  once compiled into the same `Stub.dll` and `Demo.dll`, so one failed on a file the other had open, or read the
  other's demo for its `schema.json`.
- **`tools/update-host-block.ps1` knows the earlier templates of every branch.** It read the lines of earlier host
  blocks from the checked-out history only. A project can carry the block of a branch not merged yet, and a plain
  `git log` also leaves out a merged branch whose merge kept the other side's file; such lines counted as the
  project's own.
- **`tools/docs-shots.ps1` takes Newtonsoft.Json from the preview's build.** It loaded it from a Streamer.bot folder
  that was its default on one machine only, and elsewhere the run stopped after seven of its eight windows.
  `-StreamerBotPath` is gone; no Streamer.bot folder is needed.

## [1.9.0] - 2026-11-12

The Log page's colours can be declared, and what the audit of 2026-11-08 had left open is done. The window's
part works with every game as it is. The settings action's part (stub 0.3.0) and the builder's new parts come to
a game with its next release; nothing a game has stops working.

### Added

- **A game declares the colours of its results on the Log page:** `.Log(..., styles: new[] { "GAME-OVER|muted",
  "WIN-*|success" }, outcome: new[] { "DUEL WIN|WIN" })`, in the schema `log.styles` and `log.outcome`. The colours
  were guessed from words in the result, so Hangman's `GAME-OVER` and `MISS-LTTR` showed in the accent colour. A
  style is `success`, `warning`, `muted`, `accent` or `none`; `*` stands for any run of characters; the first that
  fits wins, and a result none names is coloured by its words as before. `outcome` names what a result is filtered
  by. See *The colours of the results* in [docs/Schema.md](docs/Schema.md).
- **`ask`:** a field that asks the game whenever it changes (a preset slider whose answer fills other fields) has a
  name of its own. `live` on a field that is no button meant the same and is still read.
- **Builder:** `UiSchema.P(...).AtBottom()` (a page at the foot of the rail) and `UiWindow.InLanguage("de")` (the
  window's language); both could only be said in the schema. `UiHost.Handle` takes `null` for the action's name and
  uses the one Streamer.bot gives the run.
- **Without the mouse:** the rail takes the keyboard (Tab, Up and Down, Enter or Space). In the customise mode the
  Menu key (or Shift+F10) opens a field's or a page's menu, which has *Move up* and *Move down* now, also on
  Alt+Up and Alt+Down; the moved one keeps the keyboard.
- **A locked window can show the help under the fields:** a bulb in its footer, beside the language. The switch for
  it sits in the customise bar, which a locked window does not have.
- **Tests for the settings action.** `stub/Stub.cs` is compiled into the tests against a stand-in for Streamer.bot
  and tested there: versions, the download's tries, the files it keeps, how it asks the game.

### Fixed

- **The window does not stand still while the game is asked.** A Try button, a table and the check before a save
  waited for the game on the window's own thread, up to 35 seconds when the game's handler hung, and Windows called
  the window "not responding"; [docs/Loader.md](docs/Loader.md) said it never froze. The game is asked from another
  thread now. A Try button's answer and a table's rows arrive when they arrive (the button is greyed, the table says
  that it is asking); the check before a save waits with the window alive, without input. With every settings
  action, also the ones games carry today.
- **No answer is not "done".** A Try button whose game did not answer (a timeout, a disabled action) showed
  "done" in green; it says that the game did not answer, in the colour of a failure, and a table says so instead
  of showing as empty (stub 0.3.0).
- **An import is checked like a save.** It wrote values without their fields' limits, without a look at their
  type and without the game's `validate`, and it wrote `readOnly` settings. Every value goes through its field now
  (a number beyond its limits is pulled into them; text for a number or an option the dropdown lacks is not taken),
  a `readOnly` setting is left alone, the dialog names what is not taken and why, and the game checks as before a
  save. The dialog no longer says "replace": what the file does not name stays.
- **A value the window shows other than the file has it is said:** a number beyond its limits or text where a
  number belongs showed corrected and was written with some later save, without a word, while the game went on
  reading the file's value. The status line names how many, its tooltip which.
- **A `readOnly` setting follows its conditions and has its help:** `showWhen`, `requires`, its section's condition
  and its help (tooltip or line) were skipped for it.
- **The help under the fields reaches the framework's own fields:** the Messages page's sender switches and number
  format and the Log page's switch kept their help in the tooltip.
- **Long labels and the status line in a narrow window.** A switch's label wraps instead of being cut at the
  window's edge without "…" (German and Spanish labels at the smallest width); the status line takes a line of its
  own above the footer's buttons when they leave it no room (it read "Ke…").
- **Customise mode:** a click on *Done* or *Export* right after typing an accent colour is no longer lost (taking
  the colour rebuilt the window under the pointer); the hint counts the help switch as a change, *Reset layout*
  takes it back too and is greyed while there is nothing to take back; an empty accent box opens the picker on the
  accent in use, not on yellow.
- **Icons:** *Reset page* no longer looks like the mirror image of *Reset all* and no longer shares its arrow with
  "read again" on the Log page and the tables; the Log page's folder button has the folder a path field has.
- **`twitchOnly` works on every field with a label.** It was read for switches alone and did nothing elsewhere
  (three reward ids in Trivia): such a field says "(Twitch only)" in its label.
- **`auto` brings `live` and `panel`.** An action without a button followed the typed values only with `live` and
  wanted `panel` although it has a box of its own.
- **Settings action (stub 0.3.0):**
  - *Closing the update dialog no longer skips the version for good.* **No** skips it, **Cancel**, the X and Esc
    mean "not now". Before, No meant "later" and Cancel "skip", so the X skipped.
  - *A download GitHub does not deliver is tried three times* (a 5xx, a 429, a broken or timed-out connection),
    and what is left reads "GitHub did not deliver the file ... Try again in a moment" instead of the raw error.
  - *Every request to the game has a variable of its own,* so a request that hung and answers late cannot be read
    by the next one; it is unset also when the game's run failed.
  - *What holds for the one DLL is kept once:* a skipped version and `"autoUpdate": false` live in
    `<Streamer.bot>\UIFramework\loader.json` and hold for every game. They were kept per game, so each game asked
    again about a version another had skipped. A game's layout file is still read.
  - *Today's log lines show while a round runs:* the log was opened as a file nobody writes to, which the game's
    open file is not.
  - *One prefix in the log,* `[<game action>/Settings]` or the one the window declares, from the first line to the
    DLL's lines; it changed to the window's title half-way.
  - The release check counts `1.9` as `1.9.0`; the hint for a DLL fetched by hand no longer asks GitHub a second
    time (offline it stood still again) and does not name a release this settings action is too old for; the
    dialog no longer says the host block stands "at the end" of the action.

### Changed

- **The red i says more:** `panel`, `auto` or `confirm` on a field that is no button; `placeholders` on one that
  is no message; a `platform` that is none; a `{placeholder}` in a message's default text that its list lacks; a
  page named by its id alone that no setting is on (`"mesages"`); a `custom` page behind the generic settings
  action, which was left out with its fields in silence; a `Cmd`, `Link` or `Note` in the tour and a `Tip` in a
  help section (one type in the host block, so the wrong one compiled and was dropped); an unknown style of the
  Log page's colours. An unknown kind is told the kinds there are, and "The spec overrides" is said in the
  builder's words.
- **A schema written by hand may use the builder's short forms,** which the docs showed as if they were JSON: an
  option `"a|Label"`, a list column `"key|kind|label"`, `shows` as `"option: key, key"`, one `aliasFactor`. A bar
  segment's colour is read as `color` too.
- **Texts:** English dialog titles in one spelling; the tour names Ctrl+0; the icon hint lists `gift` and says
  that an unknown name shows a dot; a page's tooltip in the customise mode names *Change icon*.
- **Docs:** `requires` greys a field out, `showWhen` hides it (`hide:` in front of a `requires` is still read and
  no longer recommended); what the game's `UiSettings` does not know of the conventions; a table of the names a
  game carries ([docs/Getting-Started.md](docs/Getting-Started.md)); how a new major line is released
  ([docs/Loader.md](docs/Loader.md)).

### Removed

- Code nothing used: `Sidecars.Strip`, `Requirement.Holds(key, op, wanted)`, `Conventions.Aliases()` and
  `FrameworkKeys`, `LayoutFile.OverriddenPages` and `OverriddenFields`, `Schema.Defaults()` and `Aliases()`,
  `Text.Or`. The tests that went through them go the way the window goes.
- `hosts/rhymetime/spec.json` and `host-section.cs`, left from before the game declared its window.

### For 2.0.0

- The settings action reads its major line from one word, `FrameworkLine` in `stub/Stub.cs`, and a test fails
  while that word and the DLL's major version disagree. Nothing else is prepared in code: what a major version is
  to take away is planned outside this repository.

## [1.8.4] - 2026-11-12

The two points the review had left open. The DLL's part works with every game as it is; the settings action's
part (stub 0.2.1) comes to a game with its next release.

### Fixed

- **A DLL is not used by a settings action older than it asks for.** `minHostStub` in `latest.json` only kept such
  a release from being downloaded; a DLL another game had installed or loaded already was used all the same. The
  DLL now names the stub it needs (`MinHostStub` in `UIFramework.csproj`, in the file's product version as
  `1.8.4+stub.0.1.0`) and opens no window for an older one: the game's dialog says which stub it needs. The
  release workflow writes the same value into `latest.json`, so the two cannot disagree.
- **Settings action (stub 0.2.1): an installed DLL that needs a newer stub is passed over** for an older one that
  fits, before it is loaded, with a line in the log. When no installed DLL fits, the dialog says so and offers the
  download.
- **Log page: lines coming in leave the row being read where it is.** The grid kept its offset, which counts rows,
  and the newest line is the first row: every line a round in chat added pushed the rows down by one. The entry
  at the top of the grid is found again and put back there. At the very top nothing is held, so new lines show
  as they come.

### Checked

- **A table list's search and the row being edited** (the row editor's mistake of 1.8.2): the table does not have
  it. The row stays while a cell is typed in and on the way to the next cell, and the search takes it when the
  row is left. One difference stays: saving while the cell is still open ends the row's edit, so a row that no
  longer matches the search is taken then.

## [1.8.3] - 2026-11-12

Games need no update.

### Changed

- **The accent colour takes a colour's name:** `red`, `blue`, `yellow`, any of the colour names WPF knows, in any
  case (`grey` spelt either way). The swatch showed such a name already, but the window kept its old accent.
- **The accent colour takes no alpha.** `#80FF0000` was applied as plain red since 1.8.2; it is not taken any more,
  and the swatch beside the box stays on the accent in use instead of showing a colour the window does not get.
  A game's own colour settings keep their alpha.
- **The colour picker opens on a name.** With `red` in the box it started on its default yellow.
- **`latest.json` lives under its major line.** The release workflow wrote every version to `lib/v1/latest.json`,
  so a 2.0.0 would have been offered to every v1 game. It now writes `lib/v<major>/latest.json`: 2.0.0 goes to
  `lib/v2/`, which only a stub made for that line reads. The DLL's update check reads the file of its own line.

## [1.8.2] - 2026-11-11

The second part of the review: the smaller mistakes, the scripts, and the docs and pictures that had fallen
behind. Games need no update.

### Fixed

- **Customise mode: *Show* shows a field the game hides.** It only took the user's own "hidden" away, and a field
  hidden in the game's declaration has none; it now writes the "shown" the layout needs.
- **Customise mode: a page dragged down lands after the page it is dropped on.** It always went in front, so a
  page dragged one place down stayed where it was and none could become the last.
- **Customise mode: a field dropped above or below a line the page put together itself** (three numbers, two
  switches) lands above or below the whole line, as [docs/Customize.md](docs/Customize.md) says, not inside it.
- **Search: a row under a pointer at rest no longer takes the selection** from the best match, so Enter goes where
  the list says.
- **Search finds what the standard pages draw:** the sender switches and the number format of the Messages page
  and the Log page's switch. A tour stop on one of them is no longer dropped.
- **Search: Tab stays in the box while the bar is up, and Esc gives the keyboard back** to the field it came from.
- **A list's search leaves the row being typed in where it is.** Under the search "app", deleting a "p" from
  "apple" hid the row under the cursor.
- **Whole numbers show their unit** ("Every (min)"), as decimals and durations did; the timer's intervals rely on
  it. A label that names the unit already stays as it is.
- **A header pill follows the saved value when its switch is not on the window** (its page or field hidden in the
  customise mode); it said "off" for whatever it could not see.
- **The currency note and pill are in the window's language** (`currency.note`, `currency.pill`; they were English
  in German and Spanish windows).
- **A condition on a list follows the list**: `requires: "words"` lifts as the first row is added, not after save
  and reopen.
- **An old name the game lists itself is read the game's way.** The conventions' factor on `AddictedPayoutCut` was
  applied by the window even where the game listed that name without one, so window and game disagreed (Trivia).
- **Dates in an answer stay text.** A table answer or a `set` value like `2026-11-10T20:15:00` was shown as
  `11/10/2026 20:15:00`.
- **A language text with broken braces is shown as it is** instead of keeping the window from opening.
- **Colours:** `#AARRGGBB`, documented all along, is understood by the picker, which opens on the stored colour and
  keeps its alpha; `#abc` and `F1C40F` are read the same by the swatch, the picker and the accent. The accent stays
  opaque.
- **No rounding noise in saved numbers:** a decimal slider at 0.3 and a percent of 35 with a factor are saved as
  `0.3` and `0.35`, not `0.30000000000000004`.
- **Log page: a reload keeps the selected line and its detail**, and the result filter follows what the log holds
  now (a log that was empty when the window opened offered "All results" only).
- **Emote pictures:** an FFZ emote with a 1x image only has its picture, and a picture that arrives late no longer
  sits beside another code.
- **Host block: `UiSettings.Double` reads a whole number beyond `long`** instead of throwing in the game's run.

### Changed

- **Tools:** `conventions-doc.ps1 -Check` and `check-stub.ps1` notice a change in letter case alone; `check-stub.ps1`
  runs twice in one console; `update-host-block.ps1`, `copy-host-block.ps1`, `build-import.ps1` and
  `spec-to-window.ps1` take relative paths from where PowerShell stands. New: `tools/docs-shots.ps1` renders the
  screenshots of the docs again.
- **Release workflow:** one run per tag (a third tag pushed in a hurry cancelled the second), and build and tests
  are two steps, so a failed build cannot hide behind passing tests.
- **Docs:** every screenshot with the window around it is new (the search is in the header); README names the
  search, the automation helpers, triggers and methods, and no longer links a deleted file; Schema.md has the
  timer keys on the Automation page, the real shape of the `emotes` answer, and says that the game action's Execute
  Code sub-action needs a name for *Execute C# Method* (so do Getting-Started and the Help page's note).
- `hosts/chatgamble`, `hosts/slotmachine` and `hosts/rhymetime` are what the released games carry and answer.
- The tests build without analyzer warnings.

## [1.8.1] - 2026-11-11

The first part of a review of the whole framework: the mistakes a streamer can run into. Games need no update for
the window's fixes; the two in the host block reach a game with its next release.

### Fixed

- **AltGr no longer counts as Ctrl.** AltGr is Ctrl+Alt to Windows, so on a German keyboard `{` (AltGr+7) jumped to
  the seventh page of a window instead of being typed, `[`, `]` and `}` likewise in windows with eight to ten pages;
  on a Spanish one `@` and `#` went to page two and three. The window's shortcuts (Ctrl+S, Ctrl+F, Ctrl+1 to 0) now
  take Ctrl without Alt.
- **Typing is left alone by an action that follows the fields.** A window with an `auto` + `live` action (Trivia's
  difficulty line) looked at the fields 350 ms after every change, and that look ended the edit of a table cell and
  turned a half-typed name into a chip (`alice,bob` became alice, bo, b). It now reads what is there and touches
  nothing.
- **`turnTimeout` has no minimum of its own.** The conventions' `min: 5` went to every game that declared none, so
  Blackjack's "0 = no limit" was shown as 0 and saved as 5. A game that wants a floor declares it (Battleship does).
- **One game's failed release check no longer silences the update notice.** A repository without a published
  release, a renamed one or GitHub's rate limit kept the notice waiting for good, for every game and for UIFramework
  itself. Such a game is now left out of that run's notice; only UIFramework's own check (no network) still holds it.
- **A list whose file could not be read cannot be edited.** It was not written, as the red **i** said, but rows
  could be typed into it and Save answered "Saved". It is greyed out now, with the reason as its tooltip
  (`list.unreadable`, in English, German and Spanish).
- **`Tip("search")` is no mistake.** The stop was shown, but the declaration check did not know the part and put a
  problem behind the red **i**.
- **The Log page lets go of its folder when the window closes.** Its watcher was stopped only when the page was
  rebuilt; every window closed left one behind, holding the whole closed window, until Streamer.bot ended.
- **A game's own page title comes first.** `Pg("board", "Rangliste")` lost to the conventions' translation of the
  wording "Board" (the grid, "Spielfeld"), so Hangman's scoreboard page was titled "Spielfeld" in German.
- **The tour leaves Alt+F4 and Alt+Space to the window.**
- **Host block: `UiTimer.Rearm` leaves a timer off that was switched off** between its tick and the game's run;
  before, that run switched the automatic start back on.
- **Host block: the DLL is loaded the way the settings action loads it.** For the update notice and `UiLive` the
  block used `LoadFrom`, which refuses a DLL saved from a browser, did not know a folder named `v1.8.0`, and did not
  look at the `.sha256` beside the DLL. It now uses `UnsafeLoadFrom`, takes the `v`, and leaves a DLL alone that no
  longer matches its hash.
- **Demo game:** the Execute Code sub-actions of `DemoGame.sb` carry their actions' names, so an *Execute C#
  Method* sub-action elsewhere can pick them.

### Changed

- [docs/Loader.md](docs/Loader.md) and [docs/Getting-Started.md](docs/Getting-Started.md) no longer say that a game
  action never loads the DLL: it does, for the update notice and the live check, which is also why a framework
  update is usually used from the next start of Streamer.bot.

## [1.8.0] - 2026-11-10

### Added

- **Search with Ctrl+F.** Ctrl+F, or the new magnifier at the top right, opens a search bar in every window. It
  finds settings (by label, help, section, page and key), messages and pages; every word typed has to occur, in any
  order, whatever the case and the accents. Up and Down move, Enter goes there: the page comes to the front and the
  field is scrolled into view, ringed for a moment and given the cursor. Games declare nothing for it and need no
  update; `Tip("search")` adds the magnifier to a window's tour. See *The search* in
  [docs/Schema.md](docs/Schema.md).
- **`UIFramework.Preview -windowshots <folder> -search <text>`** photographs the search bar with that typed in, and
  where its first result leads.

## [1.7.0] - 2026-11-10

### Added

- **`UiAdBreak`: Twitch's ad break as a window of time for a game.** Chat stays usable while ads run, so that is
  when a round fits. `Begin` opens the window from Streamer.bot's *Ad Run* trigger for the ads' length (there is no
  trigger for their end), `Active` and `SecondsLeft` say whether ads run, `Announce` answers the *Upcoming Ad*
  trigger once per break, `RoundsThatFit` and `WithBonus` do the sums. See *Twitch's ad breaks* in
  [docs/Schema.md](docs/Schema.md).
- **An Automation page in the conventions,** with five keys for the ad break (`adBreakStart`, `adBreakAnnounce`,
  `adBreakAnnounceMinutes`, `adBreakBonusPercent`, `adBreakOnly`) in English, German and Spanish.

### Changed

- **The timer's keys (`autoStart…`) are on the Automation page** instead of Game. A game that names their page
  itself is not affected.
- Trivia and Hangman start rounds with ad breaks and have the Automation page.

## [1.6.0] - 2026-11-10

### Added

- **Custom triggers and methods are declared once.** `UiWindow.Trigger`, `Triggers` (several that share their
  arguments) and `Method` say what a game offers the streamer's own actions. `UiTriggers.Register` in `Init()`
  registers the triggers from that list, and the Help page ends with *Custom triggers* and *Methods for your own
  actions*, each with its arguments, in English, German and Spanish.
- **Fire with sample values.** A button under each trigger on the Help page has the game fire it once with its
  sample values, so an action that listens to it can be tried without playing a round.
- **`UiLive`: whether you are live.** Twitch through its API with Streamer.bot's token, YouTube by the monitored
  broadcasts, Kick through its channel page; `Live`, `Offline` or `Unknown`, never an exception. Twitch and Kick
  are asked by the DLL, since the host block must compile in an action without `System.dll`. Trivia's own check
  moved here, and Hangman's automatic rounds use it.
- **`tools\copy-host-block.ps1`**, and `-Into` / `-HostName` on `update-host-block.ps1`: the block a game carries
  goes into its other action and into `hosts\<host>\HostBlock.cs` in one step, with `-Check` to compare.

### Changed

- **A conventions key hands on its `requires`,** so the timer's intervals are greyed out with their switch in a
  game that declares them with key and default alone (already in 1.5.0, not listed there).
- Trivia, Hangman, Blackjack and Battleship declare their triggers and methods this way.

## [1.5.0] - 2026-11-10

### Added

- **A timed action on a random time and after chat lines.** Streamer.bot's timer dialog offers a fixed interval, a
  random one and "only after so many chat lines", but code can set only the interval. `UiTimer` in the host block
  keeps the other two: `Sync` brings the timed action in line with the settings (a running timer keeps its
  countdown unless the schedule was just saved), `Rearm` draws a random timer's next wait each time it fires, and
  `NoteLine` / `TakeLines` count chat lines per platform and hold a start back until there are enough. See *A timed
  action* in [docs/Schema.md](docs/Schema.md).
- **Five convention keys for it:** `autoStartEnabled`, `autoStartIntervalMinutes`, `autoStartRandom`,
  `autoStartIntervalMaxMinutes` and `autoStartMinChatLines`, with page, row, labels and help in English, German and
  Spanish. A game declares them with key and default alone.

### Changed

- **Trivia and Hangman run their automatic rounds on it** (Trivia v9.3.0, Hangman v13.3.0). The host copies of
  Trivia, Hangman, Blackjack and Battleship are the games' current ones.

## [1.4.3] - 2026-11-08

### Fixed

- **A table's columns end at its edge.** A list shown as a table (Trivia's own questions) left an empty column after
  its last one: the columns were sized as if the vertical scrollbar always showed, and the whole pixels lost a few
  more. They now share the width the rows really have, with the scrollbar only while it shows, and follow it as rows
  are added or removed; what is left over goes to the widest column.

## [1.4.2] - 2026-11-08

### Fixed

- **A field moved in the customise mode no longer splits a section.** Dropped among the fields of another section,
  it joins that section (kept in the layout file by the name the game declared, so it holds in every language);
  before, it kept its own and both headings showed twice.
- **The red i no longer blames the game for the user's layout.** "The section ... is split by another section"
  looks at the order the game declared; where the user moved fields is not the game author's to fix.

## [1.4.1] - 2026-11-08

### Changed

- **Only round versions are stable releases** (2.0.0, 3.0.0); every other version is published as a pre-release, this
  one included. The release workflow decides it from the tag.
- **Update checks count pre-releases.** The update notice and the window's new-version check take a game's newest
  published release, pre-release or not (drafts never), since nearly every version between two round ones is a
  pre-release now. The log's link goes to that release.

## [1.4.0] - 2026-11-08

### Changed

- **The update notice comes as Streamer.bot starts, in every chat, or as a Windows notification.** A game calls
  `UiUpdateNotice.Startup(...)` from its `Init()` with *Precompile on Application Start* ticked, so the notice no
  longer waits for the first use of a game. About 20 seconds after the first game starts it goes to every chat whose
  broadcaster account is connected (Twitch, YouTube, Kick); while none is, it is a Windows notification. Still once
  per Streamer.bot start, one notice for all games.
- **Games choose how they tell it:** `UiWindow.Updates(chat, chatText, toast, toastAlways, toastTitle, toastText)`,
  with `{updates}` and `{count}` in the texts; in the schema `window.updates`. Games with the same texts share one
  line. The About page's switch reads *Tell me about updates*.
- The host block of 1.3.0, which asked on ordinary runs of the game, is still answered.

## [1.3.0] - 2026-11-08

### Added

- **An update notice in chat, once per Streamer.bot start.** A few minutes after the first game of a Streamer.bot
  run is used, one line in that chat names what is new: the games used so far whose newest release is newer than
  the version they run, UIFramework when a newer release is out, and a game whose settings action is too old for
  it. Then nothing more until Streamer.bot starts again; the links are in Streamer.bot's log. The DLL asks GitHub
  in the background at most twice a day and keeps the answers in `UIFramework\updates.json`; the game action never
  waits for it. *Tell chat about updates* on the About page leaves a game out (`"updateNotices": false`). Games get
  it with the host block of this version.

## [1.2.0] - 2026-11-08

Fixes from an audit of the whole framework, and the logo on the About page.

### Added

- **The UIFramework logo on the About page.** Faint in the bottom right corner, like a watermark, so the window shows
  it is the original framework: brighter under the mouse, its tooltip names the framework's version, a click opens
  its page. It sits under the content, never over a link, in the dark or the light version for the theme.

### Fixed

- **Help under fields in named rows.** A button or switch beside other fields lines up with them again, a button's
  help line goes under the row instead of stretching its column, and every help line keeps a gap to the next field.
- **List files are never written over by mistake.** A list kept in a file of its own that the game holds open stops
  the save with the reason, instead of counting as damaged and being written from the window. A list that could not
  be read when the window opened shows its defaults, says so behind the red **i**, and is not written from that
  window.
- **Reset page** leaves lists in files of their own, transient and read-only settings alone, as *Reset all* does.
  Both dialogs say what they do: *Reset all* saves at once and keeps the file as it was as `.bak`.
- A game setting called `darkMode`, `DarkMode`, `egg`, `Egg` or `rainbow` is no longer taken for the window's theme
  and removed from the game's file.
- A condition such as `!mode=free` always held. The red **i** now names it, with the way to write it (`mode!=free`).
- A cooldown that reads a key no setting has is named behind the red **i**; before, only a line in the log said so
  on every save.
- The version check no longer fails ("Value cannot be null") for a game whose version is no number.
- *Add row* in a list with a search typed clears the search, so the new row shows.
- Host block: `UiHost.Handle` clears the window's arguments before it calls the game's handler, so an action the
  handler runs is not taken for a window request. Games get this with their next host-block update.
- The demo game no longer names the framework's repository as its `releases`, which showed an update pill in every
  demo window after each framework release.
- Docs: Getting-Started's snippet for an existing game declares its three constants and reads its settings; the
  table snippet has the `userCooldown` its cooldown reads; the DLL goes into `lib\<version>\`; what `ActionName` is
  for. Customize: *How to play*, *Log* and *About* sit at the foot of the page list; help and about lines translate.

## [1.1.0] - 2026-11-08

### Added

- **Help under fields.** A switch in the customise bar puts each field's help as a small line under it instead of
  in its tooltip; links in it can be clicked. It is kept in `uiframework.layout.json` as `"helpLines": true` and
  is off by default. Notes and lists show their help as text as before.

### Changed

- **Column headers in German and Spanish.** The read-only tables (leaderboards, statistics) translate their column
  headers like the lists and the Log page do. The words the games use for them (*Player*, *Bet*, *Change*, *After*,
  *Platform*, *Trigger*, *Guess*, *Lives*, *Answer*, *Source* and more) are in the framework's German and Spanish
  texts, so every game's Log page and lists read in the window's language without a new game release.
- `hosts/`: Hangman v13.1.1.

## [1.0.0] - 2026-11-07

The first stable release: the settings window every chat game of aaskjer's runs on (Blackjack, Battleship, Classic
Chat Gamble, Hangman, High-Low, RhymeTime, Slot Machine, Trivia), with a tour of itself, its problems behind one
red button, and a Log page that reads old and new log lines alike. From here on, the host block and the settings
action change only in ways the CHANGELOG says, and a release that breaks a game needs a new major version.

- **Problems behind a red "i".** What the game declares that does not fit, and what fails while the window is
  open (a page that cannot be built, a handler that throws, a failed save, an unreadable layout file), no longer
  show as a banner and a list on the Log or About page. A red **i** appears in the header instead; a click opens a
  panel that lists them in two groups, the failures with their time, and *Copy* puts the whole list on the
  clipboard for a bug report. Esc, the close button or a click beside it closes it. Every line still goes to
  Streamer.bot's log. The status line's "Something went wrong" points at the **i**. Preview:
  `-windowshots <folder> -problems` photographs the window with the **i**, then with its panel open.
- **A tour stop to try out.** `Tip(target, title, text, act: true)` lets the user use the ringed part while the stop
  is up: clicks go through the ring to it, keys reach it, the rest of the window stays out of reach, and the ring
  follows the part as it grows (a Try button's answer). The note says *Try it yourself*. The demo game's *Roll*
  stop is one.
- **How to play, Log and About at the foot of the rail.** The three sit together at the bottom of the page list,
  in this order, whatever order a game declares them in; a game's own bottom page goes above them.
- **The Log page reads more.** Older lines in columns (`yyyy-MM-dd HH:mm:ss RESULT  user  [R1|id] Key: value | ...`)
  show their time, result, player, round and fields instead of an empty row. A column may name several keys
  (`"Word|Word,Word was"`): the first the line has is shown. A value in one pair of single quotes loses them.
  Results read with a capital per part (*Game-Over*, *Hit-Lettr*), and column headers and the empty text translate
  through the game's `Tx` like every declared text.
- **Every chat game has a tour** and the current host block; High-Low and RhymeTime have a Log page now, and
  Hangman's log fills every column ([hosts/](hosts)).

## [0.10.0] - 2026-11-07

A tour of the window on its first open.

- **The window explains itself.** A game declares a tour in its window, `.Tour(Tip(target, title, text), ...)`:
  on the window's first open the window dims and one part after the other is ringed in the accent colour, with a
  note beside it (*Back*, *Next*, *End tour*; Esc ends it, the arrow keys and Enter step through it). A stop
  points at a setting (its page comes to the front), a page (`page:messages`; the page stays bright beside its
  rail entry), a part of the window (`rail`, `save`, `saveExit`, `cancel`, `status`, `reset`, `resetPage`,
  `theme`, `customise`, `tour`) or nothing (`start`, `end`, a note of the game's own). Parts and the standard
  pages bring their own texts in English, German and Spanish, a setting its label and help; the game's own texts
  translate through `Tx`. `"tourSeen": true` in `uiframework.layout.json` keeps it from starting again; the **?**
  at the top right shows it whenever the user wants. A stop the window cannot show (a page the user hid,
  `customise` in a locked window) is left out; a target that is neither a setting, a page nor a part is listed
  like any other mistake in the declaration. See [docs/Schema.md](docs/Schema.md#the-tour).
- **The demo game has a tour**: the pages, a setting, the *Roll* button, the Messages page, the unsaved-changes
  line, saving, resetting, the theme, the customise mode and the **?** itself.
- **Host block:** `UiSchema.Tip` and `UiWindow.Tour`. A game without a tour needs nothing new; an older DLL
  ignores a tour it does not know.
- **Preview:** `-windowshots <folder> -tour` photographs every stop of the tour (`tour-01.png`, ...).

## [0.9.13] - 2026-11-07

Spanish and every language offered, neutral credits, saving that keeps what the game wrote, a sturdier loader
(stub 0.2.0), and the review's findings fixed.

- **Spanish.** `conventions/lang/es.json` translates every known key, page, placeholder and window text, and the
  short wordings German translates.
- **Every language is offered.** The language picker lists every language there are texts for, by its own name
  (English, Deutsch, Español): the DLL's files, a file in `UIFramework\lang\` beside Streamer.bot (a new language
  or corrections, without a release), and the languages a game brings texts for. A game without texts of its own
  gets what the conventions translate. A game that names its window's language has no picker.
- **A game can translate the window itself** for a language the framework lacks: `Ui(key, text)` and
  `Ph(placeholder, help)` in `Lang`.
- **A relabelled known key keeps the game's wording** where the conventions do not translate it; before, a language
  without a conventions file showed the conventions' English label over the game's own.
- **Neutral credits.** The framework names no author: a game without credits shows none, and every About page ends
  with the links `conventions/keys.json` lists (`about.links`): the UIFramework page on aaskjer.github.io,
  Streamer.bot and *Report something that looks wrong* (the game's issues when it names its repository, else the
  framework's). `About(defaultLinks: false)` leaves them out; `authorLink` does nothing any more.
- **More said to a game's author:** a known key declared as another kind of value (it would still take the
  conventions' limits, old keys and translations), a section split by another on its page (its heading would show
  twice), an icon name the framework does not know (it shows a dot instead of the word).
- **Fixes to the fixes:** a request hung in the game action no longer blocks the next ones (taking over a window and
  reopening one work again); a list moving into its own file survives the open-time theme move and Reset all, and
  Reset all leaves lists in their own files; whole numbers stay exact past 2^53; the next save also writes a
  switch, dropdown or slider shown other than stored; a hidden number box without a number keeps its value; a list
  file unreadable at save keeps the window's copy of its untouched lists; `UiSettings.Load` survives a bad path; an
  older DLL gets "no answer" as before instead of the stub's marker; the sample thousands separator follows the
  window's language.

- **The host block needs no usings.** `stub/HostSchema.cs` names every type in full (`Newtonsoft.Json.Linq.JObject`,
  `System.IO.File`, ...), so it compiles in any game action whatever its `using` lines say, right below them, at the
  end or inside `CPHInline`, and a game's own usings can no longer clash with it (a `Path` or `Formatting` of
  another namespace). Before, the game's action had to bring seven usings, and the file pasted alone into a new
  Execute Code failed with some 150 errors ("The type or namespace name 'JObject' could not be found").
  `tools/check-stub.ps1` compiles the block alone now. A game's present block keeps working;
  `tools/update-host-block.ps1` brings the new one, and takes only what stands between the template's markers.
- **Getting-Started is copy and paste.** Five steps to the running demo game, each with the ready file to paste
  (`examples/DemoGame/DemoGame.cs`, `stub/Stub.cs`) and nothing to change, what to do when a step fails, then how
  to make it your own game and how to add the block to a game you already have. The demo game carries its host
  block below the usings like every game, and names its command by its text (`C("!roll", ...)`), so a `!roll`
  made by hand gets the cooldowns too.
- **Twitch emotes show without a Twitch login.** A fresh Streamer.bot has no Twitch token, so the window could not
  ask the Twitch API and Kappa, LUL and the other global emotes stayed without a picture. Their ids now ship in
  `conventions/emotes/twitch.json`; with a login the window still asks Twitch itself, for the channel's emotes too.
  `tools/update-twitch-emotes.ps1` refreshes the list.

- **The coins icon shows.** A page with the conventions' `coins` icon (Points & Bets) showed an empty box: 🪙 is
  missing from the emoji font WPF draws with, so it is 💰 now.
- **Docs with pictures, and the conventions published.** Getting-Started, Customize, Schema and the README show
  the window; [docs/Conventions.md](docs/Conventions.md) lists the standard pages, known keys, placeholders and
  language files, and explains how to add a language. `tools/conventions-doc.ps1` keeps its tables current, and CI
  checks them.
- **German windows show the cooldown senders again.** The Messages page's "send as bot" and "send as action"
  switches for the cooldown messages were missing in German: the message groups are translated ("Abklingzeit"),
  the cooldown group was looked for in English. It is found in the window's language now.
- **A game handler that hangs no longer freezes the window** (stub 0.2.0). A request that has started gets 30
  seconds, then the window carries on without its answer; before, it waited for good and could not be closed. A
  game that has not sent its schema for a minute no longer locks its settings until Streamer.bot restarts: the
  next run opens the window.
- **A declined save leaves nothing behind.** The window's values go into a draft; only a save that is written
  takes them over. Before, a save declined at a warning kept them in the window's copy, and after a theme or
  language switch they looked saved while the file still held the old ones.
- **A number pulled into its limits is written.** A value outside a field's min/max (or no number at all) that the
  window shows as its limit or default is written with the next save, also when the user did not touch that field;
  so is what the game's clamp changed. Before, the box said 25 and the game kept reading 50.
- **A save that cannot read the settings file stops** and says why, instead of writing every list file from the
  copy the window opened with. A list moving out of the settings file into its own lands there with the first
  save.
- **Import reads like the settings file:** old keys with their factors on any Windows language (an old 0.18 is 18 %
  on a German system too), old keys inside an object, the current key winning. It lays the import over the file as
  it is now and writes only the lists it brings.
- **Reset all** keeps read-only settings and those on pages the user hid, and the game hears of the lists in their
  own files as they are, not as defaults.
- **First install checks `minHostStub` too** (stub 0.2.0): a release that needs a newer settings action is not
  installed by an older game; "0.2.0.0" no longer counts as newer than "0.2.0".
- **Loader details** (stub 0.2.0): a DLL that no longer matches its hash is set aside as `.broken` and its version
  offered again, and an installed file that is not the published one is fetched anew; the hash file is written
  before the DLL. Every game uses the UIFramework another game already loaded, so two versions never run side by
  side, and the log says which runs. One request to the game at a time, so two runs never read each other's answer;
  the run serving a reopened window hands the one-window flag back. A `latest.json` version that is no plain number
  is refused in the release check too. A cooldown above a day is set to a day with a line in the log.
- **Host block:** `UiSettings.Long` reads a whole number exactly (no detour through a double), and `Load` never
  throws, also on a path with invalid characters.
- **Conditions compare what is stored, shown or not.** A `percent` compares as its fraction
  (`reshuffleThreshold>=0.5` for 50 %), a `duration` in seconds, an "unlimited" 0 as the number it is saved as; a
  field hidden by the user no longer compares differently. `!list` holds for an empty list.
- **The check names a comparison that never holds:** `<`, `<=`, `>`, `>=` against text, or on a field that holds
  no number. A slider on a scale (`min`, `max`, `step`) is no longer reported as "without options".
- **A huge number in the settings file breaks nothing:** a whole number with more digits than a long reads as the
  largest value instead of failing the page.
- **Saving is not stopped by fields it never writes:** a Try field left empty, or a number hidden by a `showWhen`
  or its section's condition. Fields on another page are still checked.
- **A page pattern leaves the sender switches and the log switch where they are**, so no key gets two fields that
  overwrite each other on save. A page naming one by key is listed as a problem.
- **The placeholder chips' tooltips follow a language switch**; a host's own help is kept.
- **Percent fields keep their decimals.** Leaving the box rewrites only a number outside its range, to that limit;
  12.345 stays 12.345. A value in the file that is no number shows the default instead of an empty box that blocks
  saving.
- **Customise keeps rows together.** A field dropped at the top or bottom of a field that shares a line goes above
  or below the whole line, not between its members. The log page's "log file per day" switch is greyed in the
  customise mode, which has no Save.
- **A slider's kept value goes on Reset page and `set`** also when the slider already sits on the stop it is put
  on; its label follows.
- **German windows have no English left** in the customise tag "hidden", the empty-page note, the path and colour
  tooltips, an action's "done"/"failed" and the table's "Nothing here yet"/"Read again".
- **A layout file another program holds is not lost.** It is read again a few times; if it is still held, the
  window starts from the defaults for once and does not write over it. Only a file that is no JSON goes to `.broken`.
- **Numbers the same on every Windows:** decimal boxes take a minus where the setting allows negatives and refuse a
  paste that is no number; a list cell refuses "1,000" like the table (also when saved while it has the focus) and
  shows "2.5", not "2,5"; a value an action sets finds its option on a German system.
- **Smaller fixes:** emote pictures load over HTTPS only; `update-host-block` sees a change in letter case; the
  release workflow refuses a pre-release tag before publishing and keeps the published DLL on a re-run; the demo's
  dice are thread-safe.

- **Settings action (stub 0.2.0):** a game's answer is unset from Streamer.bot's globals as soon as it is read, so
  the Twitch token in the emote sources no longer stays visible in the variables. A DLL saved from a browser loads
  (it was refused as "downloaded from the internet"). When the newest installed DLL cannot be loaded, the next
  older one opens the window. The hash a download was checked with is kept beside the DLL and checked on every
  load. A release that names a higher `minHostStub` is not offered. A game takes this with its next release, by
  pasting the new `stub/Stub.cs` as its settings action.
- **Nothing unchecked from `latest.json`.** The settings action installs no release without a `sha256`, fetches the
  DLL over `https://` only and refuses a `version` that is not a version number before it becomes a folder name.
- **The release check no longer holds the window up to 10 s.** Offline (unreachable, timed out) it does not try the
  CDN after the API; a network sign-in page instead of `latest.json` falls back to the CDN.
- **A second click brings the window to the front.** While a game's window is open (or still opening), another run
  of its settings action neither asks the game nor checks for a release again, and leaves the first run's queue alone.
- **The hint for a hand-fetched DLL shows offline too.** Its example path no longer fails on `<version>` when
  `latest.json` cannot be read.

- **A save keeps what it did not change.** The window writes the settings file as it is at that moment with the
  fields the user changed laid over it, instead of the copy it read when it opened: a viewer added from chat, a
  question suggested from chat or a status the game wrote while the window was open survive a save that did not
  touch them. A list kept in a file of its own is written only when its list changed.
- **A damaged list file is kept.** One that cannot be read is copied to `.broken` before the window writes over it,
  and a list file (a JSON array) now gets its `.bak` on every save like the settings file.
- **Numbers keep their decimals.** A number field shows up to ten decimals instead of two, so 0.005 no longer
  shows, and saves back, as 0.01, nor 0.001 as 0.
- **The language switch asks first.** In a locked window, picking another language with unsaved changes asks to
  save them, as the theme button does, instead of dropping them.
- **Numbers the same on every Windows.** Option values, dropdowns, sliders, text fields and the host block's
  `UiSettings.String`/`Double`/`Long` read and write numbers with a point, so a German system no longer turns 0.5
  into "0,5"; `Long` no longer overflows on a huge stored value.
- **A bad stored value no longer breaks a page.** A number field whose value is no number shows its default, a
  number stored as text ("0,5" too) is read, and a fraction in a whole-number field is shown instead of cut.
- **`min`/`max` hold for `int`, `long` and `duration`.** A value outside is saved as the nearest limit, and the
  field shows what was saved; a `min` below 0 lets the field take a minus.
- **A big default makes a `long` field.** A whole-number default beyond an int (also where the conventions say
  `int`) is no longer cut to 2147483647 on save.
- **Dropdowns and stop sliders keep an unknown value.** A stored value none of the options has stays as an extra
  entry, selected, and is saved back unchanged instead of the first option.
- **Number columns take numbers.** A table cell of an `int`, `long` or `double` column takes digits (and a minus,
  a decimal mark) only, and a cell that is no number goes back to what it held instead of saving 0.
- **`readOnly` in both spellings; more for the host block.** `readOnly` and `readonly` are read on a setting and a
  column. `S()` takes `readOnly`, `step` and `group`. A column flag right after the kind (`"wins|int|readonly"`,
  `"face|emote|twitch"`) is a flag, not the label.
- **`null` hides any sender switch.** An explicit `null` in `senders` hides `useBot`, `sendAction`,
  `cooldownUseBot`, `cooldownSendAction` and `cooldownGroup`, as documented; before only `numberSeparator` obeyed.
- **Conditions.** `!key` holds while a switch is off or a field is empty. A gate the window does not show is read
  from its saved value instead of counting as met. A field the user's layout moves to another section takes that
  section's condition. An answer box that moves under its row hides with its button.
- **Typed decimals are not guessed.** "1,000" or "1.000,5" in a decimal field is refused as ambiguous instead of
  read as 1.
- **A damaged layout file is kept.** `uiframework.layout.json` that is not JSON is copied to `.broken` and logged;
  a value of the wrong type in it (`"order": "2"`) reads as not set instead of breaking the window.
- **A check that got no answer asks.** When the game did not answer `validate` (a timeout, a disabled action),
  the window asks whether to save unchecked instead of taking it for "no problems" (stub 0.2.0).
- **A hung game action no longer locks the window.** The window can be opened again while a request still hangs;
  a request the window gave up on is skipped instead of run late (stub 0.2.0). An error in a click handler is
  logged and shown in the status line instead of closing the window.
- **Emote sources are asked once per window**, not once per emote cell.
- **Rebuilds no longer leak.** A section heading follows its fields through a binding; every rebuild (language,
  theme, customise mode) kept the old page alive before.
- **Two windows at once** keep their own theme, accent, rainbow and language.
- **Cooldowns are set safely** (stub 0.2.0): a misnamed key leaves the cooldown as it is instead of setting 0, one
  command that fails leaves the others set, and the game always hears of the save.
- **The stub writes the layout file carefully** (stub 0.2.0): only on a change, atomically, never over a file it
  could not read. TLS 1.2 is added without pinning the process; the download mutex is honoured; the dialogs name
  the current version's folder.
- **Customise-mode drops** check that the field or page came from this window; a message template can no longer be
  dropped on another page through the rail, a field dropped on its own page stays where it is, and a field moved
  to another page leaves its old row.
- **Release workflow** builds the tag's own commit, carries `minHostStub` over and never takes `latest.json` back.
- **Docs:** the usings the host block needs; the nested `RunAction` pitfall. `tools/update-host-block.ps1` keeps a
  `.bak` and refuses to drop lines of the game's own unless `-Force`.
- Smaller: Ctrl+S does nothing in the customise mode; a name typed into a name list but not entered counts as a
  change when closing; an answer with only `set`/`status` keeps the result panel's previous lines.
- **Text keeps its spaces.** A `string` field saves what was typed, spaces at either end included (`" | "`,
  `"🎲 "`); names, paths, colours and tokens are still trimmed.
- **Durations in minutes and hours.** A `duration` with `unit` `min` or `h` is typed and shown in that unit and
  stored in seconds (1.5 min is 90); `min`/`max` stay seconds.
- **A greyed-out field looks it** on every kind: a `requires` that does not hold also dims the field.
- **German follows the game's own wording.** A conventions key the game gave a label (or help) of its own is
  translated by the game's `Tx` for that wording; without one the conventions' German for the key is shown, as
  before. `K(key, label)` without a help keeps the conventions' help.
- **`stats` and `channelPoints` pages** have an icon and a place in the order (icon name `gift` is new).
- **Reset all keeps what the window does not show:** hidden settings and keys it has no field for.
- **A page pattern leaves a moved field alone.** A field the user moved to another page is not claimed back by
  `overlay*`; a claimed field's page is the claiming one, and a template a page claims by key leaves the Messages
  picker.
- **More checks:** a section condition for a section no setting is in, an old key that is another setting's key.
- **Aliases:** an old key that is a setting of its own keeps its value instead of being moved and reset; a dotted
  alias feeds shared keys too and its old value goes also when the canonical key is in the file.
- **Host block:** `UiSettings.Load` falls back to the `.bak` of the last save on a damaged file and says so
  (`Problem`, `FromBackup`, an optional log); a relative path starts in the Streamer.bot folder.
- **Docs:** the game action's code is its last sub-action.

## [0.9.12] - 2026-11-07

About links once, no empty logo square, a table hint that fits the table.

- The About page lists every address once: a game whose own links repeat its repository or releases, which the
  framework already shows, no longer gets them twice. Without a declared logo there is no empty square beside
  the name.
- The hint under a list table mentions the checkboxes only when the table has a switch column (a word list has
  none).

## [0.9.11] - 2026-11-07

German only where a game offers it; Lock() keeps a window as declared.

- German is optional for a game: the window offers English and only the languages the game declares with `Lang`.
  A game without `Lang("de")` is never shown half in German, whatever an old layout file asks for; the language
  picker lists only what is offered and is gone when that is English alone. `Lang("de")` with no texts offers
  German from the conventions.
- `Lock()` on the window (`"locked": true` in the schema) locks the layout for everyone: no customise mode, and a
  user's renames, moves, hidden fields and accent colour are not applied. Theme and language stay the user's; the
  language picker moves into the footer. The host block gains `Lock()`; a game takes it with its next release.

- The preview's `-windowshots <folder>` photographs the whole window per page, rail, header and footer included,
  with the page at its full height, for a game's setup guide (`-pages game,emotes`, `-names access=access--cp`).

## [0.9.10] - 2026-11-06

The customise mode in the window's real layout, fields dropped beside each other; the OBS fields on one line.

- The customise mode shows a page as the window lays it out, side by side where fields share a line, instead of
  one field per line. Dropping a field at the left or right edge of another puts it beside that one; at its top
  or bottom, on a line of its own. The layout file keeps it as `fields.<key>.row`.
- The OBS connection, scene and source sit on one line, in every game that declares `obsConnectionId`,
  `obsSceneName` and `obsSourceName` (now keys of the conventions, with a `row`). A number field on its own takes
  a third of the width, as beside two others, instead of the whole line.

## [0.9.9] - 2026-11-06

List tables edited in place; fields, switches and whole sections bound to a dropdown's options.

- The list table is edited in place, as the games' own windows did before UIFramework: a click into a cell edits
  it, a switch column is a checkbox in every row that toggles with one click, several rows can be picked and
  removed at once (Remove row or Delete). Alternating rows and column lines make the cells easy to tell apart.
  The edit panel under the table is gone.
- Fields bound to a dropdown's options, any kind of field. `showWhen` on a setting shows it only while its
  condition holds (`questionSource=igdb`); `=` and `!=` take several values (`questionSource=opentdb|wikidata`, one
  of them; `!=custom|igdb`, none of them), and `&&` joins conditions that must all hold. `shows` on a dropdown
  names per option the fields it shows. `.Section(name, showWhen:)` on the window shows a whole section, heading and
  fields, on a condition. Every condition is checked when the window opens: an unknown key or a value the dropdown
  has no option for is listed on the Log page. `requires` keeps greying out, and its `hide:` form still works. The
  host block gains `showWhen:` and `shows:` on `S(...)` and `Section(...)` on the window; a game takes them with its
  next release.

## [0.9.8] - 2026-11-06

Wide lists as tables in the style of the Log page; rows with buttons; headings that hide with their fields.

- A list with a `text` column or more than three columns is a table now, in the style of the Log page: one line
  per row, a search box above, and the picked row in full underneath to edit, every column a field of its size;
  Add row, Remove row and Restore defaults below. A row keeps the properties no column shows (a timestamp the game
  writes). Lists of up to three short columns (an emote and its weight) keep the row editor.
- The picked row of a grid (the Log page, a list table) is drawn in the accent colour instead of white on white.
- In a named `row` a button takes only the width it needs, and an action's answer box goes under the whole row; the
  box stays hidden until there is an answer. A section heading whose fields are all hidden by `hide:` hides too.
- A column label in the host block may contain a bar (`"Wrong answers (a | b | c)"`); trailing `readonly` and an
  emote platform are told apart from it. German texts for the common column names (Question, Category, Word,
  Hint, Weight, …) in the conventions.
- The preview's `-pick n` picks the n-th row of every grid before a shot, so a list table shows its edit panel.

## [0.9.7] - 2026-11-06

Lists filled by an action, searched and with read-only columns; masked fields; requirements that hide; German deeper.

- What the last hosts asked for: an action's `set` fills a `list` (its rows as an array or as JSON text), a list
  of eight rows or more gets a search box (hidden rows are still saved), a column may be `readonly` (the game
  fills it, the user reads it), a text field may be `masked: true` (dots with an eye button, saved like any
  other), and `requires` with `hide:` in front hides a field instead of greying it out. In another language the
  window now translates list column headers, the name-list placeholders, the log page's switch and the help and
  about pages line by line, through the conventions' `texts` and a game's `Tx`. The preview's `-shots` wires
  requirements and live fields like the window does, so a greyed-out or hidden field shows as such. The host
  block gains `masked:` on `S(...)` and `|readonly` on a column; a host takes it with its next release.
- Hangman (v13.0.0) and Trivia (v9.0.0) are the seventh and eighth hosts on the generic stub; `hosts/hangman` and
  `hosts/trivia` carry their blocks and schemas. Gaps seen there, for later releases: an action's `set` cannot
  fill a `list`; lists have no search, filter or read-only column; no masked field kind for secrets; `requires`
  greys out but cannot hide; list column headers and the namelist placeholder stay English in German.

## [0.9.6] - 2026-11-05

Schema problems on the Log page; Free mode from chat in every host.

- Schema problems are listed on the Log page, highlighted on top of the entries, where the host's author looks
  for what went wrong; the banner points there. A window without a log page keeps them on the About page.
- Host blocks: every host but Classic Chat Gamble gained a Free mode chat switch with a Started and an Ended
  message (Slot Machine v16.2.0, Blackjack v17.2.0, Battleship v2.2.0, RhymeTime v2.2.0, High-Low v2.1.0), High-Low
  and RhymeTime their first custom triggers. Nothing of the framework changed for it.

## [0.9.5] - 2026-11-05

Room for long emote names in a list.

- A number column of a list (`int`, `double`) is half as wide as a text one: a weight is short, the emote beside
  it is what needs the room (Slot Machine's emote lists).

## [0.9.4] - 2026-11-05

Pictures for YouTube's own emojis and for Kick emotes typed short.

- Emote cells and the emotes of a result know YouTube's own emojis: typed as `:name:` (`:face-purple-rain-drops:`),
  their picture comes from a table embedded from `conventions/emotes/youtube.json` (100 of them, gathered from
  live chats; YouTube publishes no list), which the `youtube` object of a game's `emotes` answer may extend. A
  Unicode emoji still shows itself. A Kick emote may be typed short as `ID:name`, without the `[emote:` and `]`
  chat renders it with; the picture and the name beneath a result's emote come from either spelling
  (`EmotePictures.KickCode`, `EmotePictures.NameOf`). The preview's `-settle <ms>` gives emote pictures that long
  to arrive before a `-shots` shot.

## [0.9.3] - 2026-11-05

Every host on the generic stub; the spec-carrying stub retires.

- Battleship Game is the sixth and last host on the generic stub (v2.1.0, in German, 162 of its keys translated in
  the block): the summaries, the presets, the broadcaster lookup, the leaderboard reset, the OBS setup and the
  Heat markers are answered by the game, a save hands the Click to Fire settings to a live overlay through
  `saved:`; custom triggers for every finished battle on top. `hosts/battleship` lost its `spec.json` and
  `host-section.cs`, and with the last spec-carrying host gone `stub/Stub.spec.cs` and `tools/embed-spec.ps1` are removed; `tools/check-stub.ps1` compiles the one stub
  and the demo. Gaps seen there, for a later release: the column headers of the log page and its "Write a log
  file per day" switch stay English.
- Blackjack Game is the fifth host on the generic stub (v17.1.0, in German, 215 of its keys translated in the
  block): the leaderboard reset and the OBS setup run in the game and answer with what happened, the window
  declares the cooldowns of three commands. `hosts/blackjack` lost its `spec.json`, `host-section.cs` and the
  embedded `Stub.cs`; Battleship is the last host on the spec-carrying stub.
- Slot Machine Game is the fourth host on the generic stub (v16.1.0, in German): its test spin is answered by the
  game with the engine that spins in chat, the emote pictures' sources come through `emotes:` of `UiHost.Handle`,
  and `hosts/slotmachine` lost its `spec.json` and `host-section.cs`. Gaps seen there, for a later release: the
  column headers of a list and the placeholders of a tag box ("Add a Streamer.bot group") stay English.
- RhymeTime is the third host on the generic stub (v2.1.0, in German). An action's answer may carry
  `clipboard`: text the window puts on the clipboard, since the game's thread has none (the overlay URL).
- Classic Chat Gamble is the second host on the generic stub (v25.1.0, in German too); `hosts/chatgamble` lost its
  `spec.json` and `host-section.cs`, the schema carries the window. `tools/spec-to-window.ps1` writes an answer
  that is an object as its JSON and takes the log's file pattern from the old host section.
- The host block and the stubs no longer use Regex to clean an action's name (`UiSchema.Clean`): a game action
  that ships without a reference to System.dll compiles again. A game's own code must not use a `JArray`
  collection initializer for the same reason (`new JArray(a, b)` does).
- Conventions: the two boost percentages lost the "(%)" in their labels, which the percent field adds itself.

## [0.9.2] - 2026-11-05

The window in German all the way, a game's own translations, a one-row customise bar.

- German all the way: in another language than English the window now translates what the game declared, not
  only what it left out. The conventions' key texts replace the declared label and help of those keys, pages are
  titled by id, and a new `texts` section of `conventions/lang/de.json` translates declared sections, pill
  texts, page titles, option labels and message groups by their English wording; what nothing knows stays as
  declared, the user's layout still has the last word. A game ships its own translations with
  `Window.Lang("de", K(…), Pg(…), Tx(…))`, which the schema carries under `lang` and the window lays over the
  conventions' texts. The conventions gained the keys the games share (`logToFile`, the OBS trio and
  `setupObsOverlay`, `refundFailedRedemptions`, `duelEnabled`, `resetLeaderboard`, the subscriber and VIP
  boosts, `tryBet`, `tryRole`, `rewardId`), the pages `stats` and `channelPoints`, and German for the sections
  and pills of the six games.

- The customise bar is one row: colour and language with their labels beside them, the layout and file actions
  as icon buttons with tooltips (like the footer's), *Done*, and the hint as the line underneath over the full
  width, so the bar no longer takes a third of a narrow window.

## [0.9.1] - 2026-11-04

The demo as an import, the stubs without WPF references, a points system the window knows about, and the first
host on the generic stub.

- High-Low is the first host moved to the generic stub: its window is declared in its host block (written from
  the old spec by the new `tools/spec-to-window.ps1`, which turns a host's `spec.json` and the cooldowns of its
  host section into the `UiWindow` declaration), its game action answers through `UiHost.Handle`, its settings
  action is `stub/Stub.cs`. `hosts/highlow` keeps the block and the schema; its spec files are gone.
- German: the language in the customise bar now translates the window itself (buttons, status line, dialogs,
  menus, the Messages, Log and About pages' chrome, the customise mode), not only the conventions' labels. The
  texts sit in `conventions/lang/<code>.json` under `ui`; the code reads them through `Text.T(key)`, English
  where a language lacks one. What a game declares (labels, help, the help and about pages) is shown as declared.
- Points systems: `UiCurrency.Detect(CPH, settings)` in the host block finds StreamUP's Currency Core or Tawmae's
  LOYALTY POINTS by their settings global and gives the user variable and the name of the points, the two
  settings `pointsVariable` and `pointsName` as the fallback; the describe answer carries a detected system
  (`currency`), the window shows it as a header pill and says on the two settings that they are not in use. The
  demo pays a win into the viewer's points through it. Before, every game carried the same detection of its own.
- `examples/DemoGame/DemoGame.sb`: the demo as one import (both actions, the `!roll` command with its cooldowns),
  also as an import string to paste in the demo's README. `tools/build-import.ps1` keeps an export's Execute Code
  sub-actions on the files named for them, its meta version on the game's `CurrentVersion` and the README's
  string on the export; `check-stub.ps1` fails when either is behind.
- A command in `Cooldowns(C(…))` is named by its id, as the hosts do; the generic stub also takes its text
  (`!roll`) or its name for a command made by hand, looked up in Streamer.bot, and warns when there is none. The
  demo game declares its two cooldowns; before, its `globalCooldown` was saved but never reached the command.
- The settings action tells the game which global to answer into (`uiframework.var`), so the game's
  `ActionName` constant and the action's name in Streamer.bot cannot drift apart; the host block logs a line when
  it has described its settings, the stub says when no action of that name exists, and its dialog for a game that
  did not answer says where to look.
- The stubs (`stub/Stub.cs`, `stub/Stub.spec.cs`) compile without `PresentationFramework`, `PresentationCore`
  and `WindowsBase` in the action's references: the first-run and update dialogs are Win32 message boxes
  (`user32.dll`) instead of WPF ones. A fresh action with the generic stub pasted in compiled with
  `MessageBoxButton does not exist in the namespace System.Windows` before.

## [0.9.0] - 2026-11-04

The framework as a kit: a project binds it with two files and no tooling. Hosts that still carry a spec in
their settings action run unchanged.

- The game declares its window beside its settings: `UiSchema.Window`, built with `UiWindow` and the
  shorthands `P`, `H`, `Cmd`, `Note`, `Link`, `C` in the host block, goes into the describe answer as `window`.
  A stub that brings no spec takes the window from there; a spec it brings still wins. New there: `log.files`
  (the log files' pattern) and `cooldowns` (commands whose cooldowns follow settings).
- `stub/Stub.cs` is generic: one file for every project, `GameActionName` its only line to fill in. The settings
  file, the log and the cooldowns come from the schema; a Try button, the check before a save, the work after it,
  a table's rows and the emote sources go back to the game action as `uiframework.request` (`test`, `validate`,
  `saved`, `table`, `emotes`), which `UiHost.Handle` in the host block answers. The spec-carrying template
  moved to `stub/Stub.spec.cs`; `tools/embed-spec.ps1` reads it from there.
- What the schema gets wrong (an unknown kind, a requirement on a key that is not a setting, a page naming a
  pattern no setting matches, a minimum above a maximum, a pill or override on a missing key) is shown as a
  banner over the window and listed on the About page; a settings action that cannot open its window says why in
  a dialog instead of a log line.
- The Log page follows its folder: a round played in chat shows up without the reload button (which stays). The
  folder is watched for the files the window declares; a folder that is not there yet is looked for.
- A page's `icon` given as a conventions name (`dice`, `cards`) is turned into its glyph; it was shown as the
  word.
- `tools/update-host-block.ps1` refreshes the host block in a game's file and keeps its Window and Table;
  `tools/check-stub.ps1` compiles the generic stub, the spec stub and `examples/DemoGame`, and writes the demo's
  schema for the preview (`-schema` alone now opens a window declared in the schema).
- Docs in English: `docs/Schema.md` (settings, kinds, answers, the window, the game's side; `docs/Spec.md` went
  into it), `docs/Loader.md`, `docs/Customize.md`, and `docs/Getting-Started.md` with `examples/DemoGame`.
- 49 tests; the host block compiles inside the test project against a stand-in for Streamer.bot's CPH.

## [0.8.1] - 2026-11-03

- The captions of a `slider` sit under its stops; they were spread evenly and missed the thumb.
- A field that a `set` answer changes glows in the accent colour for a moment, so the eye finds what a preset
  stop moved.
- Export and import moved from the footer into the customise mode's bar, next to *Open file*. Import there
  replaces the saved settings at once (the file as it was is kept as `.bak`) and rebuilds the window on them.

## [0.8.0] - 2026-11-03

- Export and import in the footer: export writes every setting as it stands in the window to a file of the
  user's choosing (`{ "uiframework": "settings", "host", "version", "exported", "settings" }`); import reads such a
  file, or a settings file itself, into the window as if typed, old keys through the aliases, and Save is what
  keeps it. A file from another host asks first; what the window does not know is skipped.
- Tooltips have a fixed width and wrap instead of running across the screen.
- `row` on a setting: settings naming the same row, one after the other, share one line of the page whatever
  their kind. Classic Chat Gamble's bet and role to try sit side by side; Slot Machine's platform, bet and role too.
- `slider` kind: a slider over `options` (the stored value is the stop's value, the stops captioned under the
  track) or over `min` to `max` in `step`.
- `live` on a field that is not an action: the host is asked whenever the field changes, and `set` in its answer
  fills in other fields. Classic Chat Gamble's fairness slider is back on it: a stop fills in the five numbers,
  and the odds move the slider to the preset the numbers match, or to Custom.
- A line `{ "emotes": [...], "platform": "twitch" }` in an action's answer shows the emotes as pictures with their
  names beneath. Slot Machine's test spin shows the slots that way.

## [0.7.0] - 2026-11-03

- An old key two settings share (one switch that became two, Battleship's `OverlayShowRoster`) gives both its
  value on the move; before, only the first took it.
- http(s) addresses in a `note` are links.
- `twitchOnly` on a `bool`: the switch carries the "Twitch only" pill that the sender switches have.
- `answers` in the spec: what an action would answer, for the preview and its screenshots, so an `auto` panel
  shows the real thing instead of a placeholder.
- Ctrl+0 opens the tenth page.

## [0.6.0] - 2026-11-03

- `note` kind: a paragraph of text on a page, with no value. Battleship's pages carry their explanations with it.
- An action's answer may carry `set`, values for other fields of the window, put in as if typed and saved only
  with Save, and `status`, a line for the footer. Battleship's fleet presets fill in the board and the ships
  that way.
- Battleship Game v2.0.0 runs on the framework (`hosts/battleship`).

## [0.5.0] - 2026-11-03

- `requires` takes a condition on any field, not only a switch: `numberOfSlots>=5`, `cpBetMode=fixed`,
  `mode!=free`. The field is greyed out while it does not hold and follows the gate live.
- A host can check the settings before a save: `Validate(settingsJson)` returns the problems, one per line, and
  the window asks whether to save anyway. Slot Machine names a minimum above the maximum, Free mode off without a
  points variable, the jackpot pool on with a bet of 0 to win it, and a smallest weight above the largest.
- An action's panel draws an answer given as JSON: `lines` (a line can be a big headline figure) and `bar`
  (segments with label, value, text and colour, drawn proportionally with a legend). Slot Machine's odds are a
  bar of jackpot, win and loss again, with the return of a bet of 100 as the headline.
- The `emote` field kind is rendered: a text box with the emote's picture beside it, `platform` saying where
  the picture comes from, like an `emote` column.

## [0.4.1] - 2026-11-03

- `emote` columns in a `list`: the picture of the emote beside its box. Kick from the `[emote:ID:name]` code, Twitch
  by name from the channel's and the global Twitch, 7TV, BTTV and FFZ emotes (the host hands over the broadcaster's
  id, client id and token in `EmoteSources()`; without it only the global extension sets are asked), YouTube emoji
  show themselves. Slot Machine's emote lists use them, as the old window did.

## [0.4.0] - 2026-11-03

- `action` settings can be `live` (the host gets the values as they stand in the window, unsaved ones included),
  `panel` (the answer is shown in a panel under the button instead of the footer) and `auto` (no button: the host
  is asked when the page is shown and a moment after every change in the window). Slot Machine's odds and its test
  spin are built on them.
- `transient` settings: shown in the window, never saved, never counted as a change; a live snapshot carries them.
  The bet, role and platform of a test spin are such fields.
- An alias with a dot (`PlatformEmotes.twitch`) reaches into an object of the old settings file and moves the value
  out; the object is dropped once it is empty. `UiSettings` in the host block reads such aliases too.
- Slot Machine Game v16.0.0 runs on the framework (`hosts/slotmachine`).

## [0.3.4] - 2026-11-02

- Customise mode: a field let go over another field takes that field's place, after it when it came from above
  and before it when it came from below, wherever on the field the mouse is. Before, only the lower half of the
  field below counted, and that half often sat in the auto-scroll zone at the bottom edge, where the page slid
  away under the mouse; a section of two fields could hardly be turned around. The auto-scroll zone is smaller
  and slower.

## [0.3.3] - 2026-11-01

- Customise mode: a dragged field can be let go anywhere on the page. Over the upper half of a field it lands
  before that field, over the lower half after it, over a heading or below the last field at that spot; the page
  scrolls while the drag is near its top or bottom edge.
- Customise mode: the accent colour was only written on leaving its text box, which never had the focus after the
  picker, so it fell back to empty on the next change. It is now taken once the picker closes, when typing pauses,
  on Enter and on leaving the box. Its help names where the colour shows (primary buttons, switches that are on,
  the active page in the rail, links, the customise bar).

## [0.3.2] - 2026-11-01

- Customise mode: the window no longer closes and reopens after a change. Its content is rebuilt in place on the
  same page and scroll position; the same goes for the theme switch and the reset to defaults.
- Customise mode: the Messages page takes part. Templates are listed with grip and menu and can be reordered,
  renamed, hidden and reset; they stay on their page.
- Header pill: only a framework release the user answered "No" to in the loader's dialog shows as
  `UIFramework x.y.z available` (click opens the releases). The "installed" and "ready" pills are gone, and the
  stub writes `frameworkAvailable` on "No" alone.

## [0.3.1] - 2026-11-01

- Header pill for the framework's own version next to the host's pills.
- Loader: `latest.json` is read through the GitHub contents API, with the raw URL as fallback, because the raw
  CDN caches for five minutes and ignores query strings. Every release check is logged with its outcome.

## [0.3.0] - 2026-11-01

- `list` kind: a row editor for a list of rows with typed columns (`columns`), with Add row, Restore defaults and
  a row count.
- Sidecar files: a setting with `file` (and optionally `field`) is read from and written to that file next to
  the settings file and never lands in the settings file itself, so a host's word bank or question list stays in
  the file its game already reads.
- RhymeTime v2.0.0 runs on the framework.

## [0.2.0] - 2026-11-01

- Loader in the stub: downloads the DLL on first use, offers newer releases (Yes / No / Cancel skips the version),
  `autoUpdate` and `skipVersion` in the layout file.
- Customise mode: rename, move and hide pages and fields; `uiframework.layout.json`; accent colour and language.
- Blackjack v17.0.0 runs on the framework.
