namespace CDC.Api.Domain.Entities;

/// <summary>The three reviews every profile section carries. A profile role is responsible for one of them.</summary>
public enum ReviewType
{
    /// <summary>The author review, which the technical roles own.</summary>
    Author,

    /// <summary>The policy review, which the policy roles own.</summary>
    Policy,

    /// <summary>The VA review, which the VA roles own.</summary>
    Va
}
