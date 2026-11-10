# Customer experience refresh

The guest home and customer header use a forest-green, warm-white and pale-olive palette. The customer menu displays the signed-in user's name and initials, including after profile updates. The search panel preserves the three existing rental modes; service cards select a mode and scroll to its form.

## Generated image

- Asset: `CRMS/Assets/coastal-drive-hero.png`
- Generated with the built-in `image_gen` tool, not the API/CLI fallback.
- Embedded as a WPF Resource so the application does not need network access or a file outside the project to display it.
- This is an illustrative campaign image, not a photograph of a vehicle in inventory.

Exact generation prompt:

> Use case: ads-marketing. Asset type: premium car-rental desktop app panoramic hero background, 1536x864 landscape. Create a photorealistic automotive travel campaign photograph. A refined pearl-white modern SUV without any brand badge, parked safely on a wide scenic roadside turnout above a calm turquoise bay and forested coastal mountains in Vietnam, soft warm morning sunlight, restrained natural olive greens and muted blue water, subtle filmic depth, luxury travel editorial photography with real paint reflections and detailed wheels. Composition is crucial: the complete car occupies the RIGHT HALF of the image, front three-quarter view facing left, car spans x=56% to 94%, y=40% to 76%; the LEFT 48% is quiet dark forest and soft scenery with low visual detail, reserved for white headline text added separately in code. Show mountain and bay horizon in upper third. Camera at car headlight height, 50mm perspective, premium believable photograph, no people, no other cars, no text, no logos, no typography, no watermark, no UI, no borders, no cartoon. The image should remain effective cropped to a very wide hero strip; keep car and horizon within the middle 60% of image height.

## Visual verification

Build the application or the `CRMS.PaymentChecks` project. Run the existing UI render mode with `--customer-ui <output-directory>` for 1100px/1400px customer and guest snapshots, the account popup and the customer pages. These renders use offline fixtures; they do not validate live customer login or rental data. Guest rental tabs and the intercity destination control were also checked in the running WPF app.
