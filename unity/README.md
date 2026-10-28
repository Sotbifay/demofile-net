# DemoFile для Unity

Форк [saul/demofile-net](https://github.com/saul/demofile-net), парсера демок и HTTP-трансляций CS2
на C#, со сборкой под netstandard2.1 для Unity (Mono в редакторе, IL2CPP в плеере).
Изменения лежат в ветке `unity-netstandard`. Remote оригинала называется `upstream`.

Upstream собирается только под `net9.0`, а Unity понимает только netstandard2.1.
Поэтому проекты `DemoFile` и `DemoFile.Game.Cs` собираются под обе платформы:
`net9.0;netstandard2.1`. Вариант `net9.0` ведёт себя так же, как upstream.

## Что изменено относительно upstream

- `*.csproj` — `TargetFrameworks` включает netstandard2.1, `LangVersion latest`. Для netstandard
  добавлены PolySharp, System.Collections.Immutable и System.Threading.Channels, а protobuf-net
  (в коде он не используется) отключён.
- `src/DemoFile/Polyfills/` — компилируется только под netstandard2.1:
  - `PriorityQueue` — порт 4-арной кучи из .NET, порядок элементов с равным приоритетом тот же;
  - `Math.Log2`, `Math.DivRem`, `BitConverter.UInt32BitsToSingle`, `Stopwatch.GetElapsedTime` —
    через extension-члены C# 14, поэтому места вызова в коде upstream не менялись;
  - `Stream.ReadExactlyAsync`, `List.EnsureCapacity`, `BitOperations.RoundUpToPowerOf2`,
    перегрузки `HttpClient` с `CancellationToken`;
  - атрибуты `DynamicallyAccessedMembers`, `JsonPropertyName`, `JsonDerivedType`;
  - `BroadcastSyncDtoParser` — разбор JSON из `/sync` без System.Text.Json и без рефлексии.
- `FieldPath.cs` — без `[InlineArray]`, которого нет в Mono и IL2CPP (семь полей с последовательной раскладкой).
- `FieldType.cs` — обычный `Regex` вместо `[GeneratedRegex]`.
- `HttpBroadcastReader.cs` — под netstandard использует `BroadcastSyncDtoParser`.
- `QuantizedFloatEncoding.cs`, `BitBuffer.cs` — явные приведения `(float)` в цепочках float-вычислений.
  Mono в редакторе Unity считает промежуточные float с точностью double (ECMA-335 это разрешает).
  Без приведений менялись флаги квантованных float, и разбор ломался (`Delta on non-existent entity`).
  На .NET эти приведения ничего не меняют.

## Сборка для Unity

```powershell
./unity/build-unity-plugins.ps1
```

Скрипт собирает `DemoFile.dll` и `DemoFile.Game.Cs.dll` под netstandard2.1 и копирует их вместе
с XML-документацией в `..\unity-packages\Packages\Source2Demo\Plugins`. Другую папку можно
указать через `-Plugins`. Нужен .NET SDK 10.

Зависимости в Unity-проекте ставит NuGetForUnity (`Packages/nuget-packages/packages.config`):
Google.Protobuf 3.22.1, Snappier 1.3.1, System.Collections.Immutable 8.0.0,
System.Runtime.CompilerServices.Unsafe 6.1.2 и System.Threading.Channels 8.0.0.
`System.Runtime.CompilerServices.Unsafe` обязателен: без него Unity падает при первом разборе.

У DLL в пакете в `.meta` должны быть прописаны явные настройки PluginImporter без меток.
Иначе Unity может записать их в Roslyn-анализаторы, предупредив
«A roslyn analyzer should reference netstandard version 2.0».

## Проверка

```powershell
./unity/verify/verify.ps1 -UnityExe 'C:\Program Files\Unity\Hub\Editor\6000.0.67f1\Editor\Unity.exe'
```

Скрипт разбирает CS2-демки из `demos/` по тикам: игроки, позиции и углы в виде сырых бит float,
HP, оружие, деньги, события, хэш всех сущностей и перемотку назад. Это делается сборкой net9.0,
сборкой netstandard2.1 на .NET и (с `-UnityExe`) сборкой netstandard2.1 внутри Unity в batch-режиме.
Результаты сравниваются побайтно. Рабочие файлы и временный Unity-проект создаются
в `unity/_verify/` (папка в `.gitignore`).

`14150_ag2.dem` не разбирает и сам upstream (`Unrecognised serializer field: m_serializedStatic`),
во всех вариантах ошибка одинаковая.

### IL2CPP

В `unity/verify/UnityRunner/` лежат два файла:

- `BuildAndroid.cs` собирает APK: IL2CPP, ARM64, minSdk 29 как у PICO G3, stripping Medium.
  Демки кладутся в `StreamingAssets/demos`. Файлы, залитые через adb в папку приложения,
  принадлежат пользователю `shell`, и приложение их не прочитает.
- `DumpOnStart.cs` при старте копирует демки во внутреннюю папку приложения, разбирает их
  в `persistentDataPath/out`, пишет итог в logcat (`S2D_DUMP_OK` / `S2D_DUMP_FAIL`) и показывает его на экране.

Сборка (из временного Unity-проекта, куда скопированы плагины, `link.xml` пакета, `verify/Dump.cs` и эти два файла):

```
Unity.exe -batchmode -nographics -buildTarget Android -projectPath <проект> -executeMethod BuildAndroid.Build -apk <путь.apk> -demos <папка с демками>
```

После запуска результаты забираются через
`adb pull /sdcard/Android/data/com.sotbifay.s2dverify/files/out` и сравниваются с выводом net9.0.

### Результаты на 28.10.2026

Вывод побайтно совпадает с net9.0 на всех демках:
- netstandard2.1 на .NET;
- редактор Unity 6000.0.67f1 (Mono);
- PICO G3 (Android 10, IL2CPP, arm64-v8a).

На PICO G3 разбор примерно вдвое медленнее, чем на .NET на ПК: `14011.dem` за 2,2 с против ~1 с.

## Обновление с upstream

После патча CS2 upstream обновляет схему (`Schema.cs`), и без обновления разбор новых демок ломается.

```powershell
git fetch upstream
git merge upstream/main
./unity/build-unity-plugins.ps1
./unity/verify/verify.ps1 -UnityExe '...'
git push origin unity-netstandard
```
