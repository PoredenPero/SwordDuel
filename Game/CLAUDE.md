# Sword Duel – Unity projekt

Šolski projekt: preprosta igra dvoboja z meči v Unityju (izvoz za brskalnik, Web/WebGL).
Uporabnik govori slovensko – odgovarjaj v slovenščini.

## Projekt
- Unity 6.6 (6000.6.7f1 doma, 6000.6.3f1 v šoli), URP (predloga Universal 3D)
- Igra: `Assets/SwordDuel/` – sceni `Scenes/MainMenu` (zagonska) in `Scenes/Game`; stara testna scena `Assets/sword.unity` ni v buildu
- Vse (materiali, Animator, prefabi, arene, leveli, sceni) zgradi editor skripta `SwordDuel/Editor/SwordDuelBuilder.cs` (meni Sword Duel > Build Everything). Ponovna gradnja PREPIŠE scene/prefabe v `Assets/SwordDuel` – spremembe postavitve delaj v builderju, ne ročno v sceni.
- Leveli: `SwordDuel/Levels/LevelXX.asset` + `Resources/LevelDatabase.asset`; shranjevanje v PlayerPrefs (`SaveManager`)
- Boj: `Fighter.cs` (skupen za igralca in AI, zadetek = razdalja + kot v oknu animacije), `PlayerController`, `EnemyAI` (Idle/Chase/Attack/Hurt/Dead), `GameManager`
- Ščit (samo igralec, `Fighter.canBlock`): drži desni klik → stanje "Block" (clip "deffensive stance"); blokira udarce od spredaj, po 2 blokih 15 s cooldown + kratek stagger. HUD ikona v `GameUI.Update`.
- Pisava Cinzel (`SwordDuel/Fonts`, OFL) – builder jo uporabi za naslove in gumbe
- RPG Hero materiali so Standard (roza v URP) → builder dela URP kopije v `SwordDuel/Materials`
- Repo: GitHub `PoredenPero/SwordDuel`; Unity projekt je v podmapi `Game/`
- Commit/push prek GitHub Desktop. `.gitignore` je v korenu repa (prilagojen za podmapo `Game/`, izključuje velike `.zip` datoteke iz assetov).
- Koda: C# skripte v `Assets/` (predlagana mapa `Assets/Scripts/`), urejevalnik VS Code.

## Asseti (Asset Store)
- `Assets/Modular Arenas` – modularne arene (Loafbrr)
- `Assets/RPGHero` – RPG Hero PBR HP Polyart (Dungeon Mason) – glavni lik
- `Assets/SwordAnimation` – Free Sword Animation (EEJANAI TEAM) – humanoidne animacije z mečem

## Znane težave
- `Assets/SwordAnimation/Commons/Editor/ToonShaderInstaller.cs` je onemogočen z `#if false`.
  Samodejno je nameščal `com.unity.toonshader@0.11.0-preview`, ki se v Unity 6.6.7 ne prevede (CS0308).
  Ne vklapljaj ga in ne dodajaj Toon Shaderja nazaj.

## MCP
- Paket MCP for Unity (CoplayDev) je nameščen; transport HTTP Local (http://127.0.0.1:8080).
- Unity mora biti odprt; če povezave ni: Window → MCP for Unity → Start Server.
