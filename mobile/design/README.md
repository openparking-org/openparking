# Mobile UI refresh

Design reference: [OpenParking Driver App in Stitch](https://stitch.withgoogle.com/projects/3398364609157446000).

Stitch MCP generated refined Home (`03f57acf780d4cf4a9edcc36efe3e6a8`)
and My Bookings (`a483a84bee424d859986f137ea487c7e`) screens using the
existing Monochrome Mobility design system. `stitch-*.png` are the references.

The Flutter implementation keeps the existing black-and-white palette, Plus
Jakarta Sans, navigation, API calls, booking actions and gate workflow. The
generated example vehicle badges, distances, prepaid charges and automated
barrier messages were omitted because those features are not in the app.

`preview-*.png` show the implemented widgets with test fixture data and the
production theme. Generate them from `mobile/` with:

```
flutter test --update-goldens --dart-define=UI_PREVIEWS=true test/responsive_ui_test.dart
```

Responsive tests cover all four tabs, reservation and login at 320, 390 and
800 pixels, with text scales of 100%, 150% and 200%.

The font is bundled in `assets/fonts/` under the SIL Open Font License; its
license is included alongside the font.
