using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using VaricoloredSquares;
using UnityEngine;

using Rnd = UnityEngine.Random;
using System.Text.RegularExpressions;

/// <summary>
/// On the Subject of Colored Squares
/// Created and implemented by ZekNikZ upon the foundation of ColoredSquares by Timwi
/// </summary>
public class VaricoloredSquaresModule : MonoBehaviour {
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public KMRuleSeedable RuleSeedable;
    public KMColorblindMode ColorblindMode;

    public KMSelectable[] Buttons;
    public Material[] Materials;
    public Material[] MaterialsCB;
    public Material WhiteMaterial;
    public Material BlackMaterial;
    public Light LightTemplate;

    private Light[] _lights;
    private SquareColor[] _colors;
    private bool _colorblind;
    private static readonly Color[] _lightColors = new[] { Color.red, new Color(131f / 255, 131f / 255, 1f), Color.green, Color.yellow, Color.magenta };
    private readonly SquareColor[] _colorCandidates = new SquareColor[] { SquareColor.Blue, SquareColor.Red, SquareColor.Yellow, SquareColor.Green, SquareColor.Magenta };

    // Contains the (seeded) rules
    private SquareColor[][] _table;
    private int _ruleOneDirection;
    private int _backupDirection;

    private HashSet<int> _allowedPresses;
    private HashSet<int> _updateIndices;
    private SquareColor _currentColor;
    private SquareColor _nextColor;
    private int _startingPosition;
    private SquareColor _firstStageColor; // for Souvenir
    private int _lastPress;
    private HashSet<int> _lastArea = new HashSet<int>();
    private int _pressesWithoutChange = 0;

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private Coroutine _activeCoroutine;

    void Start() {
        _moduleId = _moduleIdCounter++;
        _colorblind = ColorblindMode.ColorblindModeActive;

        GenerateRules();

        float scalar = transform.lossyScale.x;
        _lights = new Light[16];
        _colors = new SquareColor[16];

        for (int i = 0; i < 16; i++) {
            int j = i;
            Buttons[i].OnInteract += delegate { Pushed(j); return false; };
            Buttons[i].GetComponent<MeshRenderer>().material = WhiteMaterial;
            Light light = _lights[i] = (i == 0 ? LightTemplate : Instantiate(LightTemplate));
            light.name = "Light" + (i + 1);
            light.transform.parent = Buttons[i].transform;
            light.transform.localPosition = new Vector3(0, 0.08f, 0);
            light.transform.localScale = new Vector3(1, 1, 1);
            light.gameObject.SetActive(false);
            light.range = .1f * scalar;
        }

        SetInitialState();
    }

    private void SetInitialState() {
        for (int i = 0; i < 5; i++) {
            _colors[3 * i] = (SquareColor) i;
            _colors[(3 * i) + 1] = (SquareColor) i;
            _colors[(3 * i) + 2] = (SquareColor) i;
        }

        _firstStageColor = (SquareColor) Rnd.Range(0, 5);
        _colors[15] = _firstStageColor;

        Shuffle(_colors);

        _allowedPresses = new HashSet<int>();
        _currentColor = _firstStageColor;
        _startingPosition = -1;
        _lastPress = -1;

        for (int i = 0; i < 16; i++) {
            if (_colors[i] == _firstStageColor) {
                _allowedPresses.Add(i);
            }
        }

        Debug.LogFormat(@"[VaricoloredSquares #{0}] Initial state: {1}", _moduleId, string.Join(" ", _colors.Select(c => "RBGYM"[(int) c].ToString()).ToArray()));

        _activeCoroutine = StartCoroutine(SetSquareColors(delay: true));
        Debug.LogFormat(@"[VaricoloredSquares #{0}] First color to press is {1}.", _moduleId, _firstStageColor);
    }

    private IEnumerator SetSquareColors(bool delay, bool solve = false) {
        if (delay) {
            yield return new WaitForSeconds(1.75f);
        }

        IList<int> sequence;
        if (_updateIndices != null) {
            sequence = Shuffle(_updateIndices.ToList());
        } else {
            sequence = Shuffle(Enumerable.Range(0, 16).ToList());
        }

        for (int i = 0; i < sequence.Count; i++) {
            SetSquareColor(sequence[i]);
            yield return new WaitForSeconds(.03f);
        }

        if (solve) {
            Module.HandlePass();
            _activeCoroutine = null;
        } else {
            _activeCoroutine = StartCoroutine(BlinkLastSquare());
        }

        if (_updateIndices != null) {
            _updateIndices = null;
        }
    }

    private IEnumerator BlinkLastSquare() {
        bool lit = false;

        while(_lastPress != -1) {
            if (lit) {
                SetSquareColor(_lastPress);
            } else {
                SetWhite(_lastPress);
            }
            lit = !lit;
            yield return new WaitForSecondsRealtime(0.5f);
        }

        _activeCoroutine = null;
    }

    private static IList<T> Shuffle<T>(IList<T> list) {
        if (list == null) {
            throw new ArgumentNullException("list");
        }

        for (int j = list.Count; j >= 1; j--) {
            int item = Rnd.Range(0, j);
            if (item < j - 1) {
                T t = list[item];
                list[item] = list[j - 1];
                list[j - 1] = t;
            }
        }
        return list;
    }

    void SetSquareColor(int index) {
        Buttons[index].GetComponent<MeshRenderer>().material = _colorblind ? MaterialsCB[(int) _colors[index]] ?? Materials[(int) _colors[index]] : Materials[(int) _colors[index]];
        _lights[index].color = _lightColors[(int) _colors[index]];
        _lights[index].gameObject.SetActive(true);
    }

    private void SetWhite(int index) {
        Buttons[index].GetComponent<MeshRenderer>().material = WhiteMaterial;
        _lights[index].color = Color.white;
        _lights[index].gameObject.SetActive(true);
    }

    private void SetBlack(int index) {
        Buttons[index].GetComponent<MeshRenderer>().material = BlackMaterial;
        _lights[index].gameObject.SetActive(false);
    }

    private void SetAllBlack() {
        for (int i = 0; i < 16; i++) {
            SetBlack(i);
        }
    }

    private void SpreadColor(SquareColor oldColor, SquareColor newColor, int index) {
        _colors[index] = newColor;
        _updateIndices.Add(index);

        if (index - 4 >= 0 && _colors[index - 4] == oldColor) SpreadColor(oldColor, newColor, index - 4);
        if (index + 4 < 16 && _colors[index + 4] == oldColor) SpreadColor(oldColor, newColor, index + 4);
        if ((index - 1) % 4 < index % 4 && index - 1 >= 0 && _colors[index - 1] == oldColor) SpreadColor(oldColor, newColor, index - 1);
        if ((index + 1) % 4 > index % 4 && index + 1 < 16 &&_colors[index + 1] == oldColor) SpreadColor(oldColor, newColor, index + 1);
    }

    private void GenerateRules() {
        MonoRandom rnd = RuleSeedable.GetRNG();

        Debug.LogFormat(@"[VaricoloredSquares #{0}] Rule Generator: generating rules according to rule seed {1}", _moduleId, rnd.Seed);

        // Add more random spread
        for (int i = 0; i < 13; i++) {
            rnd.Next();
        }

        // Generate color pentagons
        _table = new SquareColor[5][];
        for (int i = 0; i < 5; i++) {
            _colorCandidates.Shuffle(rnd);
            _table[i] = _colorCandidates.ToArray();
            Debug.LogFormat(@"[VaricoloredSquares #{0}] Rule Generator: {1} pentagon is: {2}", _moduleId, (SquareColor) i, string.Join("-", _table[i].Select(c => "RBGYM"[(int) c].ToString()).ToArray()));
        }

        // Random spread
        rnd.Next();
        rnd.Next();
        rnd.Next();

        // Generate directions
        _ruleOneDirection = (rnd.Next(2) * 2) - 1;
        _backupDirection = (rnd.Next(2) * 2) - 1;
        Debug.LogFormat(@"[VaricoloredSquares #{0}] Rule Generator: rule one direction is: {1}", _moduleId, _ruleOneDirection == -1 ? "counter-clockwise" : "clockwise");
        Debug.LogFormat(@"[VaricoloredSquares #{0}] Rule Generator: backup rule direction is: {1}", _moduleId, _backupDirection == -1 ? "counter-clockwise" : "clockwise");
    }

    private HashSet<int> CalculateNewAllowedPresses(int index) {
        HashSet<int> result = new HashSet<int>();

        SquareColor[] pentagon = _table[(int) _colors[index]];

        List<SquareColor> adjacentColors = new List<SquareColor>();
        if (index - 4 >= 0) adjacentColors.Add(_colors[index - 4]);
        if (index + 4 < 16) adjacentColors.Add(_colors[index + 4]);
        if ((index - 1) % 4 < index % 4 && index - 1 >= 0) adjacentColors.Add(_colors[index - 1]);
        if ((index + 1) % 4 > index % 4 && index + 1 < 16) adjacentColors.Add(_colors[index + 1]);

        adjacentColors = adjacentColors.Distinct().OrderBy(c => Array.IndexOf(pentagon, c)).ToList();

        Debug.LogFormat(@"[VaricoloredSquares #{0}] Adjacent colors to button #{1} are {2}", _moduleId, index, string.Join(", ", adjacentColors.Select(c => c.ToString()).ToArray()));

        int c0, c1;
        switch (adjacentColors.Count()) {
            case 1: // press next color in circle
                c0 = Array.IndexOf(pentagon, adjacentColors[0]);
                _nextColor = pentagon[(c0 + _ruleOneDirection + 5) % 5];
                break;
            case 2:
                c0 = Array.IndexOf(pentagon, adjacentColors[0]);
                c1 = Array.IndexOf(pentagon, adjacentColors[1]);
                if (c1 - c0 == 1) { // colors are adjacent
                    _nextColor = pentagon[(c1 + 2) % 5];
                } else if (c0 - c1 == -4) { // colors are adjacent (special case)
                    _nextColor = pentagon[(c0 + 2) % 5];
                } else { // colors are split
                    if ((c0 + c1) % 2 == 0) {
                        _nextColor = pentagon[(c0 + c1) / 2];
                    } else {
                        _nextColor = pentagon[(c1 + 1) % 5];
                    }
                }
                break;
            case 3:
                List<SquareColor> nonPresentColors = _colorCandidates.Where(c => !adjacentColors.Contains(c)).OrderBy(c => Array.IndexOf(pentagon, c)).ToList();
                c0 = Array.IndexOf(pentagon, nonPresentColors[0]);
                c1 = Array.IndexOf(pentagon, nonPresentColors[1]);
                if (c1 - c0 == 1) { // colors are adjacent
                    _nextColor = pentagon[(c1 + 2) % 5];
                } else if (c0 - c1 == -4) { // colors are adjacent (special case)
                    _nextColor = pentagon[(c0 + 2) % 5];
                } else { // colors are split
                    if ((c0 + c1) % 2 == 0) {
                        _nextColor = pentagon[(c0 + c1) / 2];
                    } else {
                        _nextColor = pentagon[(c1 + 1) % 5];
                    }
                }
                break;
            case 4: // press the other color
                _nextColor = _colorCandidates.Where(c => !adjacentColors.Contains(c)).First();
                break;
            default:
                Debug.LogFormat(@"[VaricoloredSquares #{0}] Error with rule checking. Next color is red.", _moduleId);
                _nextColor = SquareColor.Red;
                break;
        }

        // Populate result
        do {
            if (_nextColor != _currentColor) {
                for (int i = 0; i < 16; i++) {
                    if (_colors[i] == _nextColor) {
                        result.Add(i);
                    }
                }
            }
            if (result.Count == 0) {
                _nextColor = pentagon[(Array.IndexOf(pentagon, _nextColor) + _backupDirection + 5) % 5];
            }
        } while (result.Count == 0);

        Debug.LogFormat(@"[VaricoloredSquares #{0}] Allowed next button presses: {1}", _moduleId, string.Join(", ", result.Select(n => n.ToString()).ToArray()));

        return result;
    }

    void Pushed(int index) {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Buttons[index].transform);
        Buttons[index].AddInteractionPunch();

        if (_activeCoroutine != null) {
            StopCoroutine(_activeCoroutine);
        }
        if (_lastPress != -1) {
            SetSquareColor(_lastPress);
        }

        if (_lastPress == -1 && _allowedPresses.Contains(index)) {
            _lastPress = index;
            _startingPosition = index;
            _allowedPresses = CalculateNewAllowedPresses(index);

            Debug.LogFormat(@"[VaricoloredSquares #{0}] Button #{1} pressed successfully. Current color is now {2}. Next color is {3}.", _moduleId, index, _currentColor, _nextColor);
            _activeCoroutine = StartCoroutine(BlinkLastSquare());
        } else if (!_allowedPresses.Contains(index)) {
            Debug.LogFormat(@"[VaricoloredSquares #{0}] Button #{1} ({2}) was incorrect at this time.", _moduleId, index, _colors[index]);

            Module.HandleStrike();

            SetAllBlack();
            SetInitialState();
        } else {
            switch (_colors[index]) {
                case SquareColor.Red:
                    Audio.PlaySoundAtTransform("redlight", Buttons[index].transform);
                    break;
                case SquareColor.Blue:
                    Audio.PlaySoundAtTransform("bluelight", Buttons[index].transform);
                    break;
                case SquareColor.Green:
                    Audio.PlaySoundAtTransform("greenlight", Buttons[index].transform);
                    break;
                case SquareColor.Yellow:
                    Audio.PlaySoundAtTransform("yellowlight", Buttons[index].transform);
                    break;
                case SquareColor.Magenta:
                    Audio.PlaySoundAtTransform("magentalight", Buttons[index].transform);
                    break;
            }

            _lastPress = index;

            _updateIndices = new HashSet<int>();
            SpreadColor(_currentColor, _colors[index], _startingPosition);
            if (_updateIndices.SetEquals(_lastArea)) {
                _pressesWithoutChange++;
            } else {
                _lastArea = _updateIndices;
                _pressesWithoutChange = 0;
            }

            _currentColor = _colors[index];

            if (_pressesWithoutChange >= 3) {
                SquareColor currentColor = _colors[index];
                while (currentColor == _colors[index]) {
                    _colors[index] = (SquareColor) Rnd.Range(0, 5);
                }
                _pressesWithoutChange = 0;
                SetBlack(index);
                Audio.PlaySoundAtTransform("colorreset", Buttons[index].transform);
            }

            if (_colors.All(c => c == _colors[0])) {
                Debug.LogFormat(@"[VaricoloredSquares #{0}] Module passed.", _moduleId);
                _allowedPresses = null;
                _activeCoroutine = StartCoroutine(SetSquareColors(delay: false, solve: true));
            } else {
                _allowedPresses = CalculateNewAllowedPresses(index);

                Debug.LogFormat(@"[VaricoloredSquares #{0}] Button #{1} pressed successfully. Current color is now {2}. Next color is {3}.", _moduleId, index, _currentColor, _nextColor);
                _activeCoroutine = StartCoroutine(SetSquareColors(delay: false));
            }
        }
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} A1 | !{0} 1 1 | !{0} colorblind";
#pragma warning restore 414

    KMSelectable[] ProcessTwitchCommand(string command) {
        if (command.Trim().Equals("colorblind", StringComparison.InvariantCultureIgnoreCase)) {
            _colorblind = !_colorblind;
            StartCoroutine(SetSquareColors(delay: false));
            return new KMSelectable[0];
        }

        Match m;
        if ((m = Regex.Match(command, @"^\s*([A-F1-6,;\s]+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success) {
            var arr = m.Groups[1].Value.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var list = new List<KMSelectable>();
            for (int i = 0; i < arr.Length; i++) {
                if (!(m = Regex.Match(arr[i], @"^([A-F])([1-6])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
                    return null;
                list.Add(Buttons[char.ToUpperInvariant(m.Groups[1].Value[0]) - 'A' + (4 * (3 - (m.Groups[2].Value[0] - '1')))]);
            }
            return list.ToArray();
        }

        return null;
    }
}
