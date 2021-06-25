# LiveSwitch Mux CLI

![build](https://github.com/liveswitch/liveswitch-mux/workflows/build/badge.svg) ![code quality](https://app.codacy.com/project/badge/Grade/9a3b33b63b254b118fcdd80e807cba8c) ![license](https://img.shields.io/badge/License-MIT-yellow.svg) ![release](https://img.shields.io/github/v/release/liveswitch/liveswitch-mux.svg)

The LiveSwitch Mux CLI combines your individual LiveSwitch Media Server recordings together into single files - one per session.

Each time the tool is run, it scans your recordings directory and looks for completed sessions. A completed session is defined as a collection of completed recordings that overlap. If an overlapping recording is still active, that session is *not* considered complete.

Sessions are scoped to the application ID and channel ID. Recordings with different channel IDs or application IDs are *not* part of the same session.

## Building

Use `dotnet publish` to create a single, self-contained file for a specific platform/architecture:

### Windows
```shell
dotnet publish src/FM.LiveSwitch.Mux -r win-x64 -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true -o win
```

### macOS
```shell
dotnet publish src/FM.LiveSwitch.Mux -r osx-x64 -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true -o osx
```

### Linux
```shell
dotnet publish src/FM.LiveSwitch.Mux -r linux-x64 -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true -o linux
```

Alternatively, use `dotnet build` to create a platform-agnostic bundle (the .NET Core runtime must be installed):

```shell
dotnet build
```

Using this approach will generate a library instead of an executable. Use `dotnet lsmux.dll` instead of `lsmux` to run it.

## Docker

Images are also hosted on [DockerHub](https://hub.docker.com/r/frozenmountain/liveswitch-mux).

```shell
docker run --rm frozenmountain/liveswitch-mux [verb] [options]
```

## Environment Variables

Environment variables can be used in place of command-line arguments.

Environment variable names are `lsmux_{option}`, e.g. `lsmux_gateway-url`.

Environment variable names are case-insensitive, so `lsmux_application-id` is equivalent to `LSMUX_APPLICATION-ID`.

Note that command-line arguments always take precedence over environment variables.

## Usage

```shell
lsmux [options]
```

```
  -i, --input-path                      The input path, i.e. the recording path used by the
                                        media server. Defaults to the current directory.

  -o, --output-path                     The output path for muxed sessions. Defaults to the
                                        input path.

  -t, --temp-path                       The path for temporary intermediate files. Defaults to
                                        the input path.

  -s, --strategy                        (Default: AutoDetect) The recording strategy used by the
                                        media server. Valid values: AutoDetect, Hierarchical,
                                        Flat

  -l, --layout                          (Default: HGrid) The video layout to use. Valid values:
                                        HStack, VStack, HGrid, VGrid, JS

  -m, --margin                          (Default: 0) The margin, in pixels, to insert between
                                        videos in the layout.

  -w, --width                           (Default: 1280) The pixel width of the output video.

  -h, --height                          (Default: 720) The pixel height of the output video.

  -f, --frame-rate                      (Default: 30) The frames per second of the output video.

  --min-orphan-duration                 (Default: 120) Minimum duration (in minutes) of waiting
                                        for incomplete media files to have file size unchanged
                                        for the media to be considered orphaned.

  --disable-json-integrity-check        (Default: false) Disable JSON integrity checks on the
                                        input path.

  --disable-orphan-session-detection    (Default: false) Disable orphan session detection on the
                                        input path.

  --background-color                    (Default: 000000) The background colour.

  --audio-codec                         (Default: libopus) The output audio codec and options.

  --video-codec                         (Default: libvpx -crf 10 -b:v 2M -auto-alt-ref 0) The
                                        output video codec and options.

  --audio-container                     (Default: mka) The output audio container (file
                                        extension).

  --video-container                     (Default: mkv) The output video container (file
                                        extension).

  --dynamic                             Dynamically update the video layout as recordings start
                                        and stop.

  --crop                                Crop video in order to use all available layout space.

  --camera-weight                       (Default: 1) How much layout space to use for camera
                                        content relative to other content. Ignored for JS
                                        layout.

  --screen-weight                       (Default: 5) How much layout space to use for screen
                                        content relative to other content. Ignored for JS
                                        layout.

  --no-audio                            Do not mux audio.

  --no-video                            Do not mux video.

  --move-inputs                         Move input files to the move path.

  --move-path                           The destination path for moved files. Defaults to the
                                        output path.

  --delete-inputs                       Delete input files from the input path.

  --no-prompt                           Do not prompt before deleting.

  --application-id                      The application ID to mux.

  --channel-id                          The channel ID to mux.

  --output-file-name                    (Default:
                                        session_{startTimestamp}_to_{stopTimestamp}_{sessionId})
                                        The output file name template. Uses curly-brace syntax
                                        (e.g. {channelId}). Valid variables: applicationId,
                                        channelId, sessionId, startTimestamp, stopTimestamp

  --js-file                             For JS layout, the JavaScript file path. Defaults to
                                        layout.js in the input path.

  --continue-on-failure                 Continue processing remaining sessions if a session
                                        fails to be processed.

  --trim-first                          Trim audio from the first participant before any other
                                        participants have joined. Requires the --no-video flag.

  --trim-last                           Trim audio from the last participant after all other
                                        participants have left. Requires the --no-video flag.

  --no-filter-files                     Do not use files for the filters. Pass the filters as
                                        arguments.

  --save-temp-files                     Do not delete the temporary intermediate files.

  --dry-run                             Do a dry-run with no muxing.

  --session-id                          The session ID to mux, obtained from a dry-run.

  --input-filter                        A regular expression used to filter the input file list.

  --input-file-names                    A comma separated list of input file names to target
                                        instead of scanning the directory.

  --input-file-paths                    A comma separated list of input file paths to target
                                        instead of scanning the directory. Overrides
                                        --input-path and --input-file-names.
```

The `input-path` to your recordings defaults to the current directory, but can be set to target another directory on disk.

```shell
lsmux --input-path /path/to/my/recordings
```

The `output-path` can be set as well. If not set, the `input-path` will be used:

```shell
lsmux --input-path /my/input/path --output-path /my/output/path
```

There are several other options available to control the behaviour and output of the muxer. For example, to create an audio-only mix with `no-video` for `channel-id` "bar" in `application-id` "foo":

```shell
lsmux --no-video --application-id foo --channel-id bar
```

Several `layout` options are available:

-   `hstack` produces a single row of videos.
-   `vstack` produces a single column of videos.
-   `hgrid` produces a grid that favours width over height when necessary.
-   `vgrid` produces a grid that favours height over width when necessary.

The `width` and `height` can also be set to the desired size, along with the `frame-rate` in frames per second:

```shell
lsmux --layout hgrid --width 1280 --height 720 --frame-rate 60
```

The `margin` between videos is configurable, as is the `background-color`, which can be any [color value](https://ffmpeg.org/ffmpeg-utils.html#Color) supported by ffmpeg. You may opt to `crop` the videos, which will increase the size of each individual recording to use all available layout space and then crop the edges as needed, while still honouring the `margin`. Layouts can be `dynamic` as well, adapting throughout the mix to changes in video size and count:

```shell
lsmux --margin 5 --background-color blue --crop --dynamic
```

You can set the `audio-codec` or `video-codec` used in the output files. Any of the [audio encoders](https://www.ffmpeg.org/ffmpeg-codecs.html#Audio-Encoders) or [video encoders](https://www.ffmpeg.org/ffmpeg-codecs.html#Video-Encoders) supported by ffmpeg are allowed, along with any codec-specific options you want to set:

```shell
lsmux --audio-codec aac --video-codec "libx264 -crf 18 -preset medium"
```

## JavaScript Layouts

The `js` layout can be used to do custom JavaScript-based layout calculations by providing a path to a `js-file`:

```shell
lsmux --layout js --js-file /path/to/my/layout.js
```

Your file must contain a JavaScript function called `layout` with the following signature:

```javascript
/**
 * @param {Input[]} inputs  The layout inputs.
 * @param {Output}  output  The layout output.
 *
 * @return {Rectangle[]}    An array of frames - the origin and size for each input
 *                          to target for rendering in the output layout.
 */
function layout(inputs, output) {
   ...
   return frames;
}
```

The `layout` function is responsible for returning an array of frames (origin/size) that the `inputs` can target for rendering. The frames must fit within the `output.size`.

For example, to arrange the input videos in a circle:

```javascript
/**
 * Apply a circular layout.
 */
function layout(inputs, output) {
    
    var center = {
        x: output.size.width / 2,
        y: output.size.height / 2
    };
    
    var radius = Math.min(output.size.width, output.size.height) / 4;
    
    // the top of the circle
    var angle = 1.5 * Math.PI;

    var frames = [];
    for (var i = 0; i < inputs.length; i++) {
        frames.push({
            origin: {
                x: (radius * Math.cos(angle) + center.x) - (radius / 2),
                y: (radius * Math.sin(angle) + center.y) - (radius / 2),
            },
            size: {
                width: radius,
                height: radius
            }
        });
        angle += (2 * Math.PI / inputs.length);
    }
    return frames;
}
```

The type definitions are as follows:

```typescript
interface Input {
  connectionId: string;
  clientId: string;
  deviceId: string;
  userId: string;
  size: Size;
  connectionTag: string;
  audioMuted: boolean;
  audioDisabled: boolean;
  videoMuted: boolean;
  videoDisabled: boolean;
}

interface Output {
  size: Size;
  margin: number;
  channelId: string;
  applicationId: string;
}

interface Rectangle {
  origin: Point;
  size: Size;
}

interface Point {
  x: number;
  y: number;
}

interface Size {
  width: number;
  height: number;
}
```

## Output

General log output is written to `stderr`, while `stdout` is reserved for a list of JSON metadata files - one for each session, e.g.:

```
/path/to/my/recordings/session_2019-12-25_07-59-37_to_2019-12-25_09-08-43_1758af02-c9bc-dc5a-1eef-41c1130a8c41.json
/path/to/my/recordings/session_2019-12-25_08-02-34_to_2019-12-25_09-03-38_1758af02-c9bc-dc5a-1eef-41c1130a8c41.json
```

Each JSON metadata file includes details about the clients, connections, and recordings that make up the session, including the input and output files, e.g.:

```json
{
  "id": "1758af02-c9bc-dc5a-1eef-41c1130a8c41",
  "channelId": "bar",
  "applicationId": "foo",
  "startTimestamp": "2019-12-15T00:50:14.4373839Z",
  "stopTimestamp": "2019-12-15T00:50:27.4008816Z",
  "file": "session_2019-12-15_00-50-14_to_2019-12-15_00-50-27.mkv",
  "audioFile": "session_2019-12-15_00-50-14_to_2019-12-15_00-50-27_audio.mka",
  "videoFile": "session_2019-12-15_00-50-14_to_2019-12-15_00-50-27_video.mkv",
  "clients": [
    {
      "id": "7b21e9ad5ba945f19d9afd8e954feca9",
      "deviceId": "bb1ecb54ff70484590d1dc975e42a1ac",
      "userId": "d6bb4ed0316c4e9cb6bb0767c0317359",
      "startTimestamp": "2019-12-15T00:50:14.4373839Z",
      "stopTimestamp": "2019-12-15T00:50:26.6333831Z",
      "connections": [
        {
          "id": "f05064e61ab84543a5218fd10b3a4256",
          "startTimestamp": "2019-12-15T00:50:14.4373839Z",
          "stopTimestamp": "2019-12-15T00:50:26.6333831Z",
          "recordings": [
            {
              "id": "92b91c41-f460-07a0-0a84-ddc666e61639",
              "audioId": "7a950878-0e12-83cc-179f-67978969046e",
              "videoId": "7f43ebd7-58ce-fc1d-c162-a2586db2d530",
              "startTimestamp": "2019-12-15T00:50:14.4373839Z",
              "stopTimestamp": "2019-12-15T00:50:26.6333831Z",
              "audioStartTimestamp": "2019-12-15T00:50:14.4373839Z",
              "audioStopTimestamp": "2019-12-15T00:50:26.6333831Z",
              "videoStartTimestamp": "2019-12-15T00:50:14.4373839Z",
              "videoStopTimestamp": "2019-12-15T00:50:26.6333831Z",
              "audioFile": "/path/to/my/recordings/f05064e61ab84543a5218fd10b3a4256-0.mka",
              "videoFile": "/path/to/my/recordings/f05064e61ab84543a5218fd10b3a4256-0.mkv",
              "logFile": "/path/to/my/recordings/f05064e61ab84543a5218fd10b3a4256-0.json",
              "videoDelay": 0.0,
              "videoSegments": [
                {
                  "size": {
                    "width": 640,
                    "height": 480
                  },
                  "audioMuted": false,
                  "audioDisabled": false,
                  "videoMuted": false,
                  "videoDisabled": false,
                  "startTimestamp": "2019-12-15T00:50:14.4373839Z",
                  "stopTimestamp": "2019-12-15T00:50:26.0603839Z"
                }
              ]
            }
          ]
        }
      ]
    },
    {
      "id": "25a29b1d5d4f41fd96c0d515d7153a21",
      "deviceId": "cbd13f6a4409477ead7885d6907cc391",
      "userId": "283e55b56ece458d81fe91b09afe41b8",
      "startTimestamp": "2019-12-15T00:50:15.4083855Z",
      "stopTimestamp": "2019-12-15T00:50:26.1053812Z",
      "connections": [
        {
          "id": "3d406343a148458bafdd3de873bb5a01",
          "startTimestamp": "2019-12-15T00:50:15.4083855Z",
          "stopTimestamp": "2019-12-15T00:50:26.1053812Z",
          "recordings": [
            {
              "id": "ecdae899-28c1-4210-81e9-9ff34c53f75d",
              "audioId": "5f4d14d4-ef97-4f33-b52b-8d72fd8b9aa3",
              "videoId": "be37fb0d-4a22-4ca2-93ea-c72a66d16b40",
              "startTimestamp": "2019-12-15T00:50:15.4083855Z",
              "stopTimestamp": "2019-12-15T00:50:26.1053812Z",
              "audioStartTimestamp": "2019-12-15T00:50:15.4083855Z",
              "audioStopTimestamp": "2019-12-15T00:50:26.1053812Z",
              "videoStartTimestamp": "2019-12-15T00:50:15.4083855Z",
              "videoStopTimestamp": "2019-12-15T00:50:26.1053812Z",
              "audioFile": "/path/to/my/recordings/3d406343a148458bafdd3de873bb5a01-0.mka",
              "videoFile": "/path/to/my/recordings/3d406343a148458bafdd3de873bb5a01-0.mkv",
              "logFile": "/path/to/my/recordings/3d406343a148458bafdd3de873bb5a01-0.json",
              "videoDelay": 0.0,
              "videoSegments": [
                {
                  "size": {
                    "width": 320,
                    "height": 240
                  },
                  "audioMuted": false,
                  "audioDisabled": false,
                  "videoMuted": false,
                  "videoDisabled": false,
                  "startTimestamp": "2019-12-15T00:50:15.4083855Z",
                  "stopTimestamp": "2019-12-15T00:50:19.2643855Z"
                },
                {
                  "size": {
                    "width": 480,
                    "height": 360
                  },
                  "audioMuted": false,
                  "audioDisabled": false,
                  "videoMuted": false,
                  "videoDisabled": false,
                  "startTimestamp": "2019-12-15T00:50:19.2973855Z",
                  "stopTimestamp": "2019-12-15T00:50:23.2643855Z"
                },
                {
                  "size": {
                    "width": 640,
                    "height": 480
                  },
                  "audioMuted": false,
                  "audioDisabled": false,
                  "videoMuted": false,
                  "videoDisabled": false,
                  "startTimestamp": "2019-12-15T00:50:23.2963855Z",
                  "stopTimestamp": "2019-12-15T00:50:24.4283855Z"
                }
              ]
            }
          ]
        }
      ]
    },
    {
      "id": "84a34515889143a7ac22ec679f347c14",
      "deviceId": "2ef4f3c7ccd84cdf874c85f03782f878",
      "userId": "1bd6f5b9bbbc44e9955ae4658c2dfbd6",
      "startTimestamp": "2019-12-15T00:50:20.0703811Z",
      "stopTimestamp": "2019-12-15T00:50:27.4008816Z",
      "connections": [
        {
          "id": "4e1b23826d7749438ab944b997a4ac2e",
          "startTimestamp": "2019-12-15T00:50:20.0703811Z",
          "stopTimestamp": "2019-12-15T00:50:27.4008816Z",
          "recordings": [
            {
              "id": "73b810d7-e3ce-4929-be04-88ae5d2da6ac",
              "audioId": "aad8aed4-a4d1-4020-bce7-89e226ea5874",
              "videoId": "1ae41ed7-e6a0-4a02-aaa3-a62390b8359f",
              "startTimestamp": "2019-12-15T00:50:20.0703811Z",
              "stopTimestamp": "2019-12-15T00:50:27.4008816Z",
              "audioStartTimestamp": "2019-12-15T00:50:20.0703811Z",
              "audioStopTimestamp": "2019-12-15T00:50:27.4008816Z",
              "videoStartTimestamp": "2019-12-15T00:50:20.0703811Z",
              "videoStopTimestamp": "2019-12-15T00:50:27.4008816Z",
              "audioFile": "/path/to/my/recordings/4e1b23826d7749438ab944b997a4ac2e-0.mka",
              "videoFile": "/path/to/my/recordings/4e1b23826d7749438ab944b997a4ac2e-0.mkv",
              "logFile": "/path/to/my/recordings/4e1b23826d7749438ab944b997a4ac2e-0.json",
              "videoDelay": 0.0,
              "videoSegments": [
                {
                  "size": {
                    "width": 1920,
                    "height": 1080
                  },
                  "connectionTag": "screen",
                  "audioMuted": false,
                  "audioDisabled": false,
                  "videoMuted": false,
                  "videoDisabled": false,
                  "startTimestamp": "2019-12-15T00:50:20.0703811Z",
                  "stopTimestamp": "2019-12-15T00:50:27.1563811Z"
                }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

## Contact

To learn more, visit [frozenmountain.com](https://www.frozenmountain.com) or [liveswitch.io](https://www.liveswitch.io).

For inquiries, contact [sales@frozenmountain.com](mailto:sales@frozenmountain.com).

All contents copyright © Frozen Mountain Software.