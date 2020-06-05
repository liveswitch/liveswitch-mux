# LiveSwitch Mux CLI

The LiveSwitch Mux CLI combines your individual LiveSwitch Media Server recordings together into single files - one per session.

Each time the tool is run, it scans your recordings directory and looks for completed sessions. A completed session is defined as a collection of completed recordings that overlap. If an overlapping recording is still active, that session is *not* considered complete.

Sessions are scoped to the application ID and channel ID. Recordings with different channel IDs or application IDs are *not* part of the same session.

## Requirements

1. ffmpeg (minimum version 4.2.1)
1. .NET Core (minimum version 3.1 LTS)

## Usage

```
dotnet lsmux.dll [options]
```

```
  -i, --input-path      The input path, i.e. the recording path used by the
                        media server. Defaults to the current directory.

  -o, --output-path     The output path for muxed sessions. Defaults to the
                        input path.

  -s, --strategy        (Default: AutoDetect) The recording strategy used by the
                        media server. Valid values: AutoDetect, Hierarchical,
                        Flat

  -l, --layout          (Default: HGrid) The video layout to use. Valid values:
                        HStack, VStack, HGrid, VGrid, JS

  -m, --margin          (Default: 0) The margin, in pixels, to insert between
                        videos in the layout.

  -w, --width           (Default: 1920) The pixel width of the output video.

  -h, --height          (Default: 1080) The pixel height of the output video.

  -f, --frame-rate      (Default: 30) The frames per second of the output video.

  --background-color    (Default: 000000) The background colour.

  --audio-codec         (Default: libopus) The output audio codec and options.

  --video-codec         (Default: libvpx -auto-alt-ref 0) The output video codec
                        and options.

  --audio-container     (Default: mka) The output audio container (file
                        extension).

  --video-container     (Default: mkv) The output video container (file
                        extension).

  --dynamic             Dynamically update the video layout as recordings start
                        and stop.

  --crop                Crop video in order to use all available layout space.

  --no-audio            Do not mux audio.

  --no-video            Do not mux video.

  --move-inputs         Move input files to the output path.

  --delete-inputs       Delete input files from the input path.

  --no-prompt           Do not prompt before deleting.

  --application-id      The application ID to mux.

  --channel-id          The channel ID to mux.

  --output-file-name    (Default: session_{startTimestamp}_to_{stopTimestamp})
                        The output file name template. Uses curly-brace syntax
                        (e.g. {channelId}). Valid variables: applicationId,
                        channelId, startTimestamp, stopTimestamp

  --js-file             For JS layout, the JavaScript file path. Defaults to
                        layout.js in the input path.

  --trim-first          (Default: false) Trim audio from the first participant
                        before any other participants have joined. Requires the
                        --no-video flag.

  --trim-last           (Default: false) Trim audio from the last participant
                        after all other participants have left. Requires the
                        --no-video flag.
```

The `input-path` to your recordings defaults to the current directory, but can be set to target another directory on disk.

```
dotnet lsmux.dll --input-path /path/to/my/recordings
```

The `output-path` can be set as well. If not set, the `input-path` will be used:

```
dotnet lsmux.dll --input-path /my/input/path --output-path /my/output/path
```

There are several other options available to control the behaviour and output of the muxer. For example, to create an audio-only mix with `no-video` for `channel-id` "bar" in `application-id` "foo":

```
dotnet lsmux.dll --no-video --application-id foo --channel-id bar
```

Several `layout` options are available:

- `hstack` produces a single row of videos.
- `vstack` produces a single column of videos.
- `hgrid` produces a grid that favours width over height when necessary.
- `vgrid` produces a grid that favours height over width when necessary.

The `width` and `height` can also be set to the desired size, along with the `frame-rate` in frames per second:

```
dotnet lsmux.dll --layout hgrid --width 1280 --height 720 --frame-rate 60
```

The `margin` between videos is configurable, as is the `background-color`, which can be any [color value](https://ffmpeg.org/ffmpeg-utils.html#Color) supported by ffmpeg. You may opt to `crop` the videos, which will increase the size of each individual recording to use all available layout space and then crop the edges as needed, while still honouring the `margin`. Layouts can be `dynamic` as well, adapting throughout the mix to changes in video size and count:

```
dotnet lsmux.dll --margin 5 --background-color blue --crop --dynamic
```

You can set the `audio-codec` or `video-codec` used in the output files. Any of the [audio encoders](https://www.ffmpeg.org/ffmpeg-codecs.html#Audio-Encoders) or [video encoders](https://www.ffmpeg.org/ffmpeg-codecs.html#Video-Encoders) supported by ffmpeg are allowed, along with any codec-specific options you want to set:

```
dotnet lsmux.dll --audio-codec aac --video-codec "libx264 -crf 18 -preset medium"
```

## JavaScript Layouts

The `js` layout can be used to do custom JavaScript-based layout calculations by providing a path to a `js-file`:

```
dotnet lsmux.dll --layout js --js-file /path/to/my/layout.js
```

Your file must contain a JavaScript function called `layout` with the following signature:

```
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

```
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

```
interface Input {
  connectionId: string;
  clientId: string;
  deviceId: string;
  userId: string;
  size: Size;
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
/path/to/my/recordings/2019-12-25_07-59-37_to_2019-12-25_09-08-43.json
/path/to/my/recordings/2019-12-25_08-02-34_to_2019-12-25_09-03-38.json
```

Each JSON metadata file includes details about the clients, connections, and recordings that make up the session, including the input and output files, e.g.:

```
{
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
              "startTimestamp": "2019-12-15T00:50:14.4373839Z",
              "stopTimestamp": "2019-12-15T00:50:26.6333831Z",
              "audioFile": "0-f05064e61ab84543a5218fd10b3a4256-0-audio.mka",
              "videoFile": "0-f05064e61ab84543a5218fd10b3a4256-0-video.mkv",
              "videoSegments": [
                {
                  "size": {
                    "width": 640,
                    "height": 480
                  },
                  "firstTimestamp": "2019-12-15T00:50:14.4373839Z",
                  "lastTimestamp": "2019-12-15T00:50:26.0603839Z"
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
              "startTimestamp": "2019-12-15T00:50:15.4083855Z",
              "stopTimestamp": "2019-12-15T00:50:26.1053812Z",
              "audioFile": "0-3d406343a148458bafdd3de873bb5a01-0-audio.mka",
              "videoFile": "0-3d406343a148458bafdd3de873bb5a01-0-video.mkv",
              "videoSegments": [
                {
                  "size": {
                    "width": 320,
                    "height": 240
                  },
                  "firstTimestamp": "2019-12-15T00:50:15.4083855Z",
                  "lastTimestamp": "2019-12-15T00:50:19.2643855Z"
                },
                {
                  "size": {
                    "width": 480,
                    "height": 360
                  },
                  "firstTimestamp": "2019-12-15T00:50:19.2973855Z",
                  "lastTimestamp": "2019-12-15T00:50:23.2643855Z"
                },
                {
                  "size": {
                    "width": 640,
                    "height": 480
                  },
                  "firstTimestamp": "2019-12-15T00:50:23.2963855Z",
                  "lastTimestamp": "2019-12-15T00:50:24.4283855Z"
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
              "startTimestamp": "2019-12-15T00:50:20.0703811Z",
              "stopTimestamp": "2019-12-15T00:50:27.4008816Z",
              "audioFile": "0-4e1b23826d7749438ab944b997a4ac2e-0-audio.mka",
              "videoFile": "0-4e1b23826d7749438ab944b997a4ac2e-0-video.mkv",
              "videoSegments": [
                {
                  "size": {
                    "width": 640,
                    "height": 480
                  },
                  "firstTimestamp": "2019-12-15T00:50:20.0703811Z",
                  "lastTimestamp": "2019-12-15T00:50:27.1563811Z"
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