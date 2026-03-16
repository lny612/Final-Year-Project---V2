using System;

/// <summary>
/// Plain data class representing a generated customer dossier.
/// Shared by CustomerGenerator and MaterialGenerator.
/// </summary>
[Serializable]
public class CustomerOrder
{
    public string customerName;
    public string schoolOfMagic;
    public string profession;
    public string personality;
    public string request;
    public string trueGoal;
    public string constraint;
}
