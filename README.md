# Live2DFrequencyLipSync
Implementation of [__Web-based live speech-driven lip-sync__ (_Llorach et al. 2016_)](https://repositori.upf.edu/bitstream/handle/10230/28139/llorach_VSG16_web.pdf) for Unity to be used in games with _Live2D Cubism_.
The original lip-sync implementation in the [Live2D Cubism SDK](https://github.com/Live2D/CubismUnityComponents) uses only the voice volume to determine how much the mouth of the character should open. This approach works very well, but more detail/realism can be added if the shape of the mouth is also taken into account. 

The algorithm implemented in this example is very simple and can easily run in real-time on most devices.

__Demo video:__ https://www.youtube.com/watch?v=wGsSn093m2U

## How does this work?
For technical details, refer to the original paper: https://repositori.upf.edu/bitstream/handle/10230/28139/llorach_VSG16_web.pdf

## How to use?
`MouthShapeAnalyzer` takes an `AudioSource` for audio input and one `Animator` that should have the four parameters _"closed"_, _"open"_, _"pressed"_ and _"kiss"_, corresponding to the shapes defined in the paper.
Alternatively, another `AudioSource` can be supplied if you want to get input directly from a microphone. In that case, the _Use-Microphone_-checkbox must be checked.

Create the four blend shapes for your character as described in the paper. To do this, you can simply create 1-frame-animations in the Live2D Cubism editor and export them to Unity. After importing them, set them up as an additional animation layer (blending: override, weight: 1). This layer should by default play a blend-tree animation set up like this:

![Example](https://raw.githubusercontent.com/DenchiSoft/Live2DFrequencyLipSync/master/animator_example.png "Example")

The parameters for `MouthShapeAnalyzer` have to be set individually for each characters, because of differences in voice pitch. I recommend just trying out different values until you find something that looks good.  
