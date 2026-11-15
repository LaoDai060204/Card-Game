# Battle Spirits asset and design review workflow

## User agreement

The user designated `review/battle-spirits-english-cards-20261115` as the ongoing
place to collect the Battle Spirits assets and designs they request. The user
has authorized uploading that requested review work to this branch. Proceed
with routine requested asset updates without asking again for upload approval.

During this design stage, produce reviewable assets and previews. Start Unity
integration and game implementation when the user later requests that work.
Do not treat completion of an asset batch or a generic request to continue its
review as an instruction to begin the Unity conversion.

## Branch scope

- Keep this branch focused on requested Battle Spirits resources, designs,
  previews, source references, and their documentation.
- Add card art, UI/field designs, models, animation, audio, and other resources
  as requested. Clearly distinguish planned resources from completed files.
- The current completed batch is 61 unofficial English card mockups, their
  original reference images, four Standard decklists, and the review viewer.
- Preserve the existing `main`, `mdpro3-english`, and
  `battle-spirits-mdpro3-ui` branches and the current Unity project during the
  review stage. The intended later code baseline is MDPro3 English on
  `mdpro3-english`; no new Unity conversion is started by this agreement alone.
- This review branch has an independent root and contains no Unity project.
  For later implementation, copy the selected resources into a development
  branch based on the Unity baseline. Do not merge this entire review history
  over the game project or replace the Unity checkout with this asset branch.

## Update and verification

- Before a batch changes existing review files, preserve the current review
  commit on a uniquely named `backup/YYYY-MM-DD-description` branch, push it,
  and verify the remote commit. Never overwrite backups or force-push history.
- Keep original source images and provenance separate from localized or newly
  created variants. Preserve source attribution and distinguish unofficial
  translations or mockups from official assets.
- The accepted card presentation is option B: English directly on the card,
  including names, families, effects, flavor text, and Romanized artist names.
  The user requested removal of the publisher/manufacturing footer from the
  English visual mockups; original images and accompanying attribution remain.
- Preserve rule data and numeric stats when editing presentation. Validate
  counts, image references, preview loading, and the actual changed assets.
- Publish the requested update to this review branch and verify its remote
  commit and usable GitHub image/download links. Do not add a new mandatory
  approval step for ordinary review updates already requested by the user.
- Do not publish credentials, private local configuration, Unity caches, or
  unrelated runtime assets. Report precisely what was produced and checked.
