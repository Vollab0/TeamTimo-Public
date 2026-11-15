using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Masque le modèle du joueur local (son ombre reste) quand la caméra est poussée tout contre lui,
/// par exemple quand il est dos à un mur : sans cela, sa tête remplit l'écran.
/// À poser sur la Main Camera, avec la CinemachineFreeLook qui porte le CinemachineCollider.
/// </summary>
[DefaultExecutionOrder(10000)] // après le CinemachineBrain : on lit la position finale de la caméra
[DisallowMultipleComponent]
public class CameraTargetHider : MonoBehaviour
{
    [Tooltip("Caméra 3e personne dont on suit la cible (Follow = joueur local). Vide = trouvée au démarrage")]
    [SerializeField] private CinemachineFreeLook freeLook;
    [Tooltip("En dessous de cette distance entre la caméra et le point visé (LookAt), le joueur est masqué (m)")]
    [SerializeField] private float hideDistance = 4f;
    [Tooltip("Marge avant de réafficher le joueur, pour éviter le clignotement (m)")]
    [SerializeField] private float showMargin = 0.5f;

    private Transform _target;
    private Renderer[] _renderers = new Renderer[0];
    private ShadowCastingMode[] _originalModes = new ShadowCastingMode[0];
    private bool _hidden;

    private void Awake()
    {
        if (freeLook == null) freeLook = FindFirstObjectByType<CinemachineFreeLook>();
    }

    private void LateUpdate()
    {
        Transform follow = freeLook != null ? freeLook.Follow : null;
        if (follow != _target) BindTarget(follow);
        if (_target == null) return;

        Transform lookAt = freeLook.LookAt != null ? freeLook.LookAt : _target;
        float distance = Vector3.Distance(transform.position, lookAt.position);
        if (!_hidden && distance < hideDistance) SetHidden(true);
        else if (_hidden && distance > hideDistance + showMargin) SetHidden(false);
    }

    private void OnDisable()
    {
        SetHidden(false);
    }

    /// <summary>Nouvelle cible (apparition du joueur local) : on garde ses renderers une fois pour toutes.</summary>
    private void BindTarget(Transform target)
    {
        SetHidden(false);
        _target = target;
        if (target == null)
        {
            _renderers = new Renderer[0];
            _originalModes = new ShadowCastingMode[0];
            return;
        }

        // Seulement les modèles (pas les VFX de fumée, déjà sans ombre).
        List<Renderer> list = new List<Renderer>();
        foreach (Renderer r in target.GetComponentsInChildren<Renderer>(true))
        {
            if (r is SkinnedMeshRenderer || r is MeshRenderer) list.Add(r);
        }
        _renderers = list.ToArray();
        _originalModes = new ShadowCastingMode[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) _originalModes[i] = _renderers[i].shadowCastingMode;
    }

    private void SetHidden(bool hidden)
    {
        if (_hidden == hidden) return;
        _hidden = hidden;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            _renderers[i].shadowCastingMode = hidden
                ? ShadowCastingMode.ShadowsOnly
                : _originalModes[i];
        }
    }
}
