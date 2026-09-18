# Character scene / material replacement guide

The mod currently falls back to the vanilla Silent scenes for these paths.
When real scenes are ready, create the files below and point them in
`Core/Character/Setsuna/Setsuna.cs`,
`Core/Character/Touma/Touma.cs`, and
`Core/Character/AlbumCharacterArt.cs`.

## Setsuna

- `res://STS_WhiteAlbum2/scenes/combat/setsuna_combat.tscn` — combat creature visuals
- `res://STS_WhiteAlbum2/scenes/char_select/select_bg_setsuna.tscn` — character-select background
- `res://STS_WhiteAlbum2/scenes/ui/character_icons/setsuna_icon.tscn` — character icon scene
- `res://STS_WhiteAlbum2/ui/energy_counters/setsuna_energy_counter.tscn` — energy counter
- `res://STS_WhiteAlbum2/scenes/shop/setsuna_shop.tscn` — merchant-room character
- `res://STS_WhiteAlbum2/scenes/rest_site/setsuna_rest_site.tscn` — rest-site character
- `res://STS_WhiteAlbum2/scenes/vfx/card_trail_setsuna.tscn` — card-trail VFX
- `res://STS_WhiteAlbum2/materials/transitions/setsuna_transition_mat.tres` — transition material

## Touma

- `res://STS_WhiteAlbum2/scenes/combat/touma_combat.tscn` — combat creature visuals
- `res://STS_WhiteAlbum2/scenes/char_select/select_bg_touma.tscn` — character-select background
- `res://STS_WhiteAlbum2/scenes/ui/character_icons/touma_icon.tscn` — character icon scene
- `res://STS_WhiteAlbum2/ui/energy_counters/touma_energy_counter.tscn` — energy counter
- `res://STS_WhiteAlbum2/scenes/shop/touma_shop.tscn` — merchant-room character
- `res://STS_WhiteAlbum2/scenes/rest_site/touma_rest_site.tscn` — rest-site character
- `res://STS_WhiteAlbum2/scenes/vfx/card_trail_touma.tscn` — card-trail VFX
- `res://STS_WhiteAlbum2/materials/transitions/touma_transition_mat.tres` — transition material

Until then, the fallback is the vanilla Silent set:

- `res://scenes/creature_visuals/silent.tscn`
- `res://scenes/screens/char_select/char_select_bg_silent.tscn`
- `res://scenes/ui/character_icons/silent_icon.tscn`
- `res://scenes/combat/energy_counters/silent_energy_counter.tscn`
- `res://scenes/merchant/characters/silent_merchant.tscn`
- `res://scenes/rest_site/characters/silent_rest_site.tscn`
- `res://scenes/vfx/card_trail_silent.tscn`
- `res://materials/transitions/silent_transition_mat.tres`
