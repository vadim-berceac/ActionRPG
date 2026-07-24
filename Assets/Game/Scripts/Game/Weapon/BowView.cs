using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class BowView : MonoBehaviour
{
    [SerializeField] private LineRenderer bowstringLine;

    [SerializeField] private Transform limb01;
    [SerializeField] private Transform limb02;

    [SerializeField] private Transform tip01;
    [SerializeField] private Transform tip02;
    [SerializeField] private Transform nockPoint;

    [SerializeField] private Transform bowstringAnchorPoint;
    [SerializeField] private AnimationCurve bowReleaseCurve;

    [Header("Timings")]
    [SerializeField] private AnimTiming loadTiming;
    [SerializeField] private AnimTiming releaseTiming;
    [SerializeField] private AnimTiming cancelTiming;
    
    [Header("Sounds")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioStruct loadSound;
    [SerializeField] private AudioStruct releaseSound;
    [SerializeField] private AudioStruct cancelSound;

    [Header("Bowstring redraw")]
    [SerializeField] private float bowstringUpdateInterval;

    private Vector3 _nockPointRestLocalPosition;
    private Vector3 _initialLimb01LocalEulerAngles;
    private Vector3 _initialLimb02LocalEulerAngles;

    private CancellationTokenSource _animationCts;
    private CancellationTokenSource _bowstringLoopCts;

    private void OnEnable()
    {
        if (nockPoint)
        {
            _nockPointRestLocalPosition = nockPoint.localPosition;
        }

        if (limb01 && limb02)
        {
            _initialLimb01LocalEulerAngles = limb01.localEulerAngles;
            _initialLimb02LocalEulerAngles = limb02.localEulerAngles;
        }

        if (bowstringLine)
        {
            bowstringLine.positionCount = 3;
        }

        UpdateBowstringLine();

        _bowstringLoopCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        BowstringUpdateLoopAsync(_bowstringLoopCts.Token).Forget();
    }
    

    private void OnDisable()
    {
        CancelCurrentAnimation();

        _bowstringLoopCts?.Cancel();
        _bowstringLoopCts?.Dispose();
        _bowstringLoopCts = null;
    }

    private void OnDestroy()
    {
        CancelCurrentAnimation();

        _bowstringLoopCts?.Cancel();
        _bowstringLoopCts?.Dispose();
        _bowstringLoopCts = null;
    }

    public void LoadBow()
    {
        if (_animationCts != null)
        {
            nockPoint.localPosition = _nockPointRestLocalPosition;
        }

        var token = RestartAnimation();
        LoadBowAsync(loadTiming.Delay, loadTiming.Duration, token).Forget();

        if (loadSound.AudioClip)
        {
            audioSource.PlayOneShot(loadSound.AudioClip, loadSound.Volume);
        }
    }

    public void ReleaseBow()
    {
        if (_animationCts != null)
        {
            nockPoint.position = bowstringAnchorPoint.position;
        }

        var token = RestartAnimation();
        ShootArrowAsync(releaseTiming.Delay, releaseTiming.Duration, token).Forget();
        
        if (releaseSound.AudioClip)
        {
            audioSource.PlayOneShot(releaseSound.AudioClip, releaseSound.Volume);
        }
    }

    public void CancelLoadBow()
    {
        var token = RestartAnimation();
        CancelLoadBowAsync(cancelTiming.Delay, cancelTiming.Duration, token).Forget();
        
        if (cancelSound.AudioClip)
        {
            audioSource.PlayOneShot(cancelSound.AudioClip, cancelSound.Volume);
        }
    }
    
    private void UpdateBowstringLine()
    {
        if (!bowstringLine || !tip01 || !tip02 || !nockPoint)
        {
            return;
        }

        bowstringLine.SetPosition(0, tip01.position);
        bowstringLine.SetPosition(1, nockPoint.position);
        bowstringLine.SetPosition(2, tip02.position);
    }

   
    private async UniTaskVoid BowstringUpdateLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var cancelled = await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, token)
                .SuppressCancellationThrow();
            if (cancelled)
            {
                return;
            }

            UpdateBowstringLine();

            if (bowstringUpdateInterval <= 0f)
            {
                continue;
            }
            
            var delayCancelled = await UniTask.Delay(
                    TimeSpan.FromSeconds(bowstringUpdateInterval), cancellationToken: token)
                .SuppressCancellationThrow();

            if (delayCancelled)
            {
                return;
            }
        }
    }
    
    private CancellationToken RestartAnimation()
    {
        CancelCurrentAnimation();

        _animationCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        return _animationCts.Token;
    }

    private void CancelCurrentAnimation()
    {
        if (_animationCts == null)
        {
            return;
        }

        _animationCts.Cancel();
        _animationCts.Dispose();
        _animationCts = null;
    }

    private async UniTaskVoid LoadBowAsync(float delay, float duration, CancellationToken token)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token)
            .SuppressCancellationThrow();

        if (token.IsCancellationRequested)
        {
            return;
        }

        var limb01LoadLocalEulerAngles =
            new Vector3(_initialLimb01LocalEulerAngles.x, _initialLimb01LocalEulerAngles.y, _initialLimb01LocalEulerAngles.z - 15f);
        var limb02LoadLocalEulerAngles =
            new Vector3(_initialLimb02LocalEulerAngles.x, _initialLimb02LocalEulerAngles.y, _initialLimb02LocalEulerAngles.z - 15f);

        nockPoint.localPosition = _nockPointRestLocalPosition;

        float t = 0;
        while (t < 1)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            t += Time.deltaTime / duration;
            limb01.localEulerAngles = Vector3.Lerp(_initialLimb01LocalEulerAngles, limb01LoadLocalEulerAngles, t);
            limb02.localEulerAngles = Vector3.Lerp(_initialLimb02LocalEulerAngles, limb02LoadLocalEulerAngles, t);

            nockPoint.position = Vector3.Lerp(nockPoint.position, bowstringAnchorPoint.position, t);

            var cancelled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            if (cancelled)
            {
                return;
            }
        }
    }

    private async UniTaskVoid ShootArrowAsync(float delay, float duration, CancellationToken token)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token)
            .SuppressCancellationThrow();

        if (token.IsCancellationRequested)
        {
            return;
        }

        var limb01LoadLocalEulerAngles =
            new Vector3(_initialLimb01LocalEulerAngles.x, _initialLimb01LocalEulerAngles.y, _initialLimb01LocalEulerAngles.z - 15f);
        var limb02LoadLocalEulerAngles =
            new Vector3(_initialLimb02LocalEulerAngles.x, _initialLimb02LocalEulerAngles.y, _initialLimb02LocalEulerAngles.z - 15f);

        var initialNockRestLocalPosition = nockPoint.localPosition;

        float t = 0;
        while (t < 1)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            t += Time.deltaTime / duration;
            limb01.localEulerAngles =
                Vector3.LerpUnclamped(limb01LoadLocalEulerAngles, _initialLimb01LocalEulerAngles, bowReleaseCurve.Evaluate(t));
            limb02.localEulerAngles =
                Vector3.LerpUnclamped(limb02LoadLocalEulerAngles, _initialLimb02LocalEulerAngles, bowReleaseCurve.Evaluate(t));

            nockPoint.localPosition =
                Vector3.LerpUnclamped(initialNockRestLocalPosition, _nockPointRestLocalPosition, bowReleaseCurve.Evaluate(t));

            var cancelled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            if (cancelled)
            {
                return;
            }
        }
    }

    private async UniTaskVoid CancelLoadBowAsync(float delay, float duration, CancellationToken token)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token)
            .SuppressCancellationThrow();

        if (token.IsCancellationRequested)
        {
            return;
        }

        var limb01LoadLocalEulerAngles =
            new Vector3(_initialLimb01LocalEulerAngles.x, _initialLimb01LocalEulerAngles.y, _initialLimb01LocalEulerAngles.z - 15f);
        var limb02LoadLocalEulerAngles =
            new Vector3(_initialLimb02LocalEulerAngles.x, _initialLimb02LocalEulerAngles.y, _initialLimb02LocalEulerAngles.z - 15f);

        var initialNockRestLocalPosition = nockPoint.localPosition;

        float t = 0;
        while (t < 1)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            t += Time.deltaTime / duration;
            limb01.localEulerAngles = Vector3.LerpUnclamped(limb01LoadLocalEulerAngles, _initialLimb01LocalEulerAngles, t);
            limb02.localEulerAngles = Vector3.LerpUnclamped(limb02LoadLocalEulerAngles, _initialLimb02LocalEulerAngles, t);

            nockPoint.localPosition = Vector3.LerpUnclamped(initialNockRestLocalPosition, _nockPointRestLocalPosition, t);

            var cancelled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            if (cancelled)
            {
                return;
            }
        }
    }
}

[System.Serializable]
public struct AnimTiming
{
    [field: SerializeField] public float Delay { get; set; }
    [field: SerializeField] public float Duration { get; set; }
}