// Artifact.cs
// Base class for all artifacts in the system

using UnityEngine;
using System;
using System.Collections.Generic;

public abstract class Artifact : MonoBehaviour {
    public string ArtifactName { get; private set; }

    // Signal system for communication
    public event Action<string, object> OnSignal;

    // Observable properties system
    private Dictionary<string, object> observableProperties = new Dictionary<string, object>();
    public event Action<string, object> OnPropertyChanged;
    private GameObject _currentUser = null;

    public GameObject GetCurrentUser() => _currentUser;
    
    protected virtual void Start()
    {
        ArtifactName = gameObject.name;
        Init();
    }

    // Init(): Initialization method to be overridden by subclasses
    protected abstract void Init();

    // Use(int agentId, params object[] args): Method used by agents to interact with the artifact
    public abstract void Use(int agentId, params object[] args);

    // DefineObsProperty(string propertyName, object initialValue): Define an observable property with an initial value
    protected void DefineObsProperty(string propertyName, object initialValue)
    {
        observableProperties[propertyName] = initialValue;
    }

    // UpdateObsProperty(string propertyName, object value): Update an observable property and notify listeners
    protected void UpdateObsProperty(string propertyName, object value)
    {
        if (observableProperties.ContainsKey(propertyName))
        {
            observableProperties[propertyName] = value;
            OnPropertyChanged?.Invoke(propertyName, value);
        }
        else
            Debug.LogWarning($"[{ArtifactName}] Tried to update undefined observable property: {propertyName}");
    }

    // GetObsProperty(string propertyName): Get the value of an observable property
    protected object GetObsProperty(string propertyName)
    {
        if (observableProperties.TryGetValue(propertyName, out var value))
            return value;
        Debug.LogWarning($"[{ArtifactName}] Observable property not found: {propertyName}");
        return null;
    }

    // EmitSignal(string name, object data): Method to emit direct signals to other artifacts
    public void EmitSignal(string name, object data)
    {
        Debug.Log($"[{ArtifactName}] Emitting signal {name}");
        OnSignal?.Invoke(name, data);
    }

    /// <summary>
    /// Restituisce true se l'artefatto è in uso da un agente diverso da quello passato.
    /// </summary>
    public bool IsInUseByOtherAgent(GameObject agent)
    {
        return _currentUser != null && _currentUser != agent;
    }

    /// <summary>
    /// Registra l'agente come utente corrente dell'artefatto.
    /// </summary>
    public bool SetCurrentUser(GameObject agent)
    {
        if (_currentUser != null && _currentUser != agent)
        {
            Debug.Log($"[Artifact] {ArtifactName} SetCurrentUser REJECTED - already in use by {_currentUser.name}");
            return false;
        }
        _currentUser = agent;
        Debug.Log($"[Artifact] {ArtifactName} SetCurrentUser: {agent.name}\n{System.Environment.StackTrace}");
        return true;
    }

    /// <summary>
    /// Rilascia l'artefatto. Chiamato quando l'agente finisce l'interazione.
    /// </summary>
    public void ReleaseUser(GameObject agent)
    {
        Debug.Log($"[Artifact] {ArtifactName} ReleaseUser called by {agent.name}, currentUser: {_currentUser?.name ?? "none"}\n{System.Environment.StackTrace}");
        if (_currentUser == agent)
        {
            _currentUser = null;
            Debug.Log($"[Artifact] {ArtifactName} Released by {agent.name}");
        }
    }

    /// <summary>
    /// Reset a fine episodio.
    /// </summary>
    public void ResetAccess()
    {
        Debug.Log($"[Artifact] {ArtifactName} ResetAccess called\n{System.Environment.StackTrace}");
        _currentUser = null;
    }
}
