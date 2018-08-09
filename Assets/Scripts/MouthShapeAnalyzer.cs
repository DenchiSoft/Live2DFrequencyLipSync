using UnityEngine;

/*
 * MouthShapeAnalyzer - Generates mouth shape weights from audio data frequency analysis.
 * Based on:
 * https://repositori.upf.edu/bitstream/handle/10230/28139/llorach_VSG16_web.pdf
 */
public class MouthShapeAnalyzer : MonoBehaviour
{
    // If true, microphone will be used instead of character voice.
    [SerializeField]
    private bool _useMicrophone = false;

    public bool UseMicrophone
    {
        get { return _useMicrophone; }
        set { _useMicrophone = value; }
    }

    // Target mouth animator.
    // Must contain the parameters "closed", "open", "pressed" and "kiss".
    [SerializeField]
    private Animator _mouthAnimator;
    public Animator MouthAnimator
    {
        get { return _mouthAnimator; }
        set { _mouthAnimator = value; }
    }

    // Processed samples (spectrum).
    // Contains the smoothed short-term power spectrum density.
    private float[] _samples;

    // Current raw samples (spectrum).
    private float[] _samplesRaw;

    // Last raw samples for smoothing.
    private float[] _lastSamples;

    // Samples from t-2 for smoothing.
    private float[] _lastSamples2;

    // Audio source for character voice.
    // This is used if UseMicrophone is set to false.
    [SerializeField]
    private AudioSource _characterVoiceAudioSource;

    public AudioSource CharacterVoiceAudioSource
    {
        get { return _characterVoiceAudioSource; }
        set { _characterVoiceAudioSource = value; }
    }

    // Audio source for microphone input.
    // This is used if UseMicrophone is set to true.
    [SerializeField]
    private AudioSource _microphoneAudioSource;

    public AudioSource MicrophoneAudioSource
    {
        get { return _microphoneAudioSource; }
        set { _microphoneAudioSource = value; }
    }

    // Currently active audio source.
    private AudioSource _activeAudioSource;

    // Smoothing factor for samples. 
    [SerializeField, Range(0, 1)]
    private float _smoothing = 0.39f;

    public float Smoothing
    {
        get { return _smoothing; }
        set { _smoothing = value; }
    }

    // Sensitivity. Higher => more sensitive.
    [SerializeField, Range(-0.5f, 0.5f)]
    private float _sensitivityThreshold = 0.43f;

    public float SensitivityThreshold
    {
        get { return _sensitivityThreshold; }
        set { _sensitivityThreshold = value; }
    }

    // Vocal tract length factor. This is needed to account for pitch differences in voices.
    // Should be around for 0.8 for men and 1.0 for women. 1.2-1.3 works well for very high voices.
    [SerializeField, Range(0.1f, 3.0f)]
    private float _vocalTractLengthFactor = 1.21f;

    public float VocalTractLengthFactor
    {
        get { return _vocalTractLengthFactor; }
        set { _vocalTractLengthFactor = value; }
    }

    // Bounding frequencies for the energy bins.
    // They will get scaled by the vocal tract length factor.
    private int[] _voiceBoundingFrequencies = new int[5] { 0, 500, 700, 3000, 6000 };

    // Blend shape weight for the "kiss" shape.
    [SerializeField, Range(0, 1)]
    private float _blendKissShape;

    // Blend shape weight for the "lips closed" shape.
    [SerializeField, Range(0, 1)]
    private float _blendLipsClosedShape;

    // Blend shape weight for the "mouth open" shape.
    [SerializeField, Range(0, 1)]
    private float _blendMouthOpenShape;

    // Blend shape weight for the "lips pressed" shape.
    [SerializeField, Range(0, 1)]
    private float _blendLipsPressedShape;

    // Enum for sample count.
    public enum AudioSampleCount { Low = 256, Medium = 512, High = 1024}

    // Sample count for FFT.
    [SerializeField]
    AudioSampleCount _sampleCount = AudioSampleCount.High;

    public AudioSampleCount SampleCount
    {
        get { return _sampleCount; }
        set { _sampleCount = value; }
    }

    // Window type for FFT. Blackman is recommended.
    [SerializeField]
    FFTWindow _windowType = FFTWindow.Blackman;

    public FFTWindow WindowType
    {
        get { return _windowType; }
        set { _windowType = value; }
    }

    // Indices into the sample array, corresponding to the bin bounding frequencies.
    private int[] _frequencyDataIndices;

    // Energy sum for each bin interval.
    private float[] _binEnergy;

    // If true, visualization of energy in each bin will be shown in inspector.
    [SerializeField]
    private bool _visualizeBinEnergy;

    // Visualization of energy in each bin.
    [SerializeField]
    private string[] _binEnergyVisualization;

    // Called by Unity for initialization.
    public void Awake()
    {
        _samples = new float[(int)_sampleCount];
        _samplesRaw = new float[(int)_sampleCount];
        _lastSamples = new float[(int)_sampleCount];
        _lastSamples2 = new float[(int)_sampleCount];

        _frequencyDataIndices = new int[5];
        _binEnergy = new float[5];
        _binEnergyVisualization = new string[4];

        for (int i = 0; i < _voiceBoundingFrequencies.Length; i++)
        {
            // Scale bounding frequencies with vocal tract length factor.
            _voiceBoundingFrequencies[i] = (int) (_voiceBoundingFrequencies[i] * VocalTractLengthFactor);

            // Calculate indices (corresponding to sample array) for those frequencies.
            _frequencyDataIndices[i] = (int) (2 * (int) _sampleCount / ((float) AudioSettings.outputSampleRate) * _voiceBoundingFrequencies[i]);
        }

        // Check if microphone should be used instead of audio file.
        if (_useMicrophone)
        {
            if (_characterVoiceAudioSource != null)
            {
                _characterVoiceAudioSource.Stop();
            }

            if (_microphoneAudioSource != null)
            {
                _microphoneAudioSource.clip = Microphone.Start(Microphone.devices[0], true, 10, 44100);
                _activeAudioSource = _microphoneAudioSource;
                _microphoneAudioSource.Play();
            }
        }
        else
        {
            _activeAudioSource = _characterVoiceAudioSource;
        }
    }

    // Called by Unity.
    void Update()
    {
        if (_activeAudioSource == null) return;

        // FFT with Blackman window.
        _activeAudioSource.GetSpectrumData(_samplesRaw, 0, _windowType);

        float oneMinusSmoothing = 1 - Smoothing;

        for (int i = 0; i < (int) _sampleCount; i++)
        {
            // Smoothing with previous frequency data.
            _lastSamples[i] = Smoothing * _lastSamples2[i] + oneMinusSmoothing * _lastSamples[i];
            _samplesRaw[i] = Smoothing * _lastSamples[i] + oneMinusSmoothing * _samplesRaw[i];

            // Compute Y[k] in dB, should be between -25dB and -160db for properly scaled speech signals.
            _samples[i] = 20 * Mathf.Log10(_samplesRaw[i]);

            // Map range to interval [-0.5, 0.5]
            _samples[i] = SensitivityThreshold + (_samples[i] + 20) / 140f;

            // Copy old samples for smoothing.
            _lastSamples2[i] = _lastSamples[i];
            _lastSamples[i] = _samplesRaw[i];
        }

        // Calculate energy for each bin.
        for (int i = 0; i < _binEnergy.Length - 1; i++)
        {
            int indexStart = _frequencyDataIndices[i];
            int indexEnd = _frequencyDataIndices[i + 1];
            float binLength = indexEnd - indexStart;

            for (int j = indexStart; j < indexEnd; j++)
            {
                _binEnergy[i] += _samples[j] > 0 ? _samples[j] : 0;
            }

            _binEnergy[i] /= binLength;
        }

        // Calculate blend shape weight from bin energy.
        // Note: These equations have been slightly altered. See comments below for original equations. 

        // Kiss shape.
        if (_binEnergy[1] >= 0.2f)
        {
            _blendKissShape = Mathf.Clamp01(1 - 3 * _binEnergy[2]);
            // _blendKissShape = Mathf.Clamp01(1 - 2 * _binEnergy[2]);
        }
        else
        {
            _blendKissShape = Mathf.Clamp01((1 - 3 * _binEnergy[2]) * 5 * _binEnergy[1]);
            // _blendKissShape = Mathf.Clamp01((1 - 2 * _binEnergy[2]) * 5 * _binEnergy[1]);
        }

        // Lips closed shape.
        _blendLipsPressedShape = Mathf.Clamp01(3f * _binEnergy[3] + 2 * _binEnergy[2]);
        // _blendLipsPressedShape = Mathf.Clamp01(3f * _binEnergy[3]);

        // Mouth open shape
        _blendMouthOpenShape = Mathf.Clamp01(0.8f * (_binEnergy[1] - _binEnergy[3]) + _binEnergy[2]);
        //_blendMouthOpenShape = Mathf.Clamp01(0.8f * (_binEnergy[1] - _binEnergy[3]));

        // Default mouth shape.
        _blendLipsClosedShape = Mathf.Clamp01(1 - _blendKissShape - _blendLipsPressedShape - _blendMouthOpenShape);

        // Set calculated weights in animator.
        MouthAnimator.SetFloat("closed", _blendLipsClosedShape);
        MouthAnimator.SetFloat("open", _blendMouthOpenShape);
        MouthAnimator.SetFloat("kiss", _blendKissShape);
        MouthAnimator.SetFloat("pressed", _blendLipsPressedShape);

        // Visualize energy in each bin for debugging.
        if (_visualizeBinEnergy)
        {
            for (int i = 0; i < _binEnergyVisualization.Length; i++)
            {
                _binEnergyVisualization[i] = new string('|', (int) (_binEnergy[i] * 64));
            }
        }
    }
}

