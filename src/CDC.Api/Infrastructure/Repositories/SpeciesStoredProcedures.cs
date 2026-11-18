namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Names of the Surveillance Profiles stored procedures backing the species feature. These are
/// the same procedures the legacy <c>Profiles.DataAccess.Sql.SpeciesDataService</c> called.
/// </summary>
public static class SpeciesStoredProcedures
{
    /// <summary>Returns every species and species group.</summary>
    public const string GetAllSpecies = "spgaSpecies";

    /// <summary>Returns the species selected for a named disease filter.</summary>
    public const string GetAllSelectedSpecies = "spgaSelectedDiseaseSpecies";

    /// <summary>Returns sections, questions and fields as three result sets.</summary>
    public const string GetSpeciesMetadata = "spgaSpeciesSectionMetadata";

    /// <summary>Returns the row version, section list and field values as three result sets.</summary>
    public const string GetSpeciesAnswerData = "spgSpeciesAnswerData";

    /// <summary>Checks the row version and returns the new one.</summary>
    public const string UpdateSpeciesAnswerData = "spuSpeciesAnswerData";

    /// <summary>Inserts, updates or deletes a single field value.</summary>
    public const string UpdateSpeciesFieldValue = "spuSpeciesFieldValue";

    /// <summary>Removes every stored value for a multi-value field.</summary>
    public const string DeleteSpeciesFieldMultiValue = "spdSpeciesFieldMultiValue";

    /// <summary>Inserts one value of a multi-value field.</summary>
    public const string InsertSpeciesFieldMultiValue = "spiSpeciesFieldMultiValue";

    /// <summary>Recalculates species prioritisation scores after an answer change.</summary>
    public const string CalculatePrioritisationScore = "sppSpeciesPrioritisationScore";

    /// <summary>Returns one species' name/parent detail and row version.</summary>
    public const string GetSpeciesById = "spgSpeciesById";

    /// <summary>Returns the species that are a legal parent choice for another species.</summary>
    public const string GetSpeciesValidParents = "spgSpeciesValidParents";

    /// <summary>Updates a species' name and parent, and writes the audit trail entry.</summary>
    public const string UpdateSpecies = "spuSpecies";

    /// <summary>Inserts a new species, and writes the audit trail entry.</summary>
    public const string InsertSpecies = "spiSpecies";

    /// <summary>Returns every recorded species name/parent change.</summary>
    public const string GetSpeciesAuditTrail = "spgaSpeciesTableAuditLog";

    /// <summary>Name used when logging a failed <see cref="ChangeSpeciesPosition"/> batch.</summary>
    public const string ChangeSpeciesPositionName = "ChangeSpeciesPosition";

    /// <summary>
    /// Swaps a species with its previous or next sibling and writes the audit trail entry. An inline
    /// batch rather than <c>spuSpeciesSequenceNumber</c>, so it works whatever version of that
    /// procedure an environment has. Siblings are renumbered 1..n first, so gaps, duplicates and
    /// NULL sequence numbers in legacy data cannot block a move.
    /// </summary>
    public const string ChangeSpeciesPosition = """
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        DECLARE @Empty uniqueidentifier = '00000000-0000-0000-0000-000000000000';
        DECLARE @ParentId uniqueidentifier;
        DECLARE @Name varchar(50);
        DECLARE @ParentName varchar(50);
        DECLARE @OldPos int;
        DECLARE @NewPos int;
        DECLARE @Siblings TABLE (Id uniqueidentifier PRIMARY KEY, Pos int NOT NULL);

        BEGIN TRANSACTION;

        SELECT @ParentId = [ParentId], @Name = [Name] FROM [Species] WHERE [Id] = @SpeciesId;

        INSERT INTO @Siblings (Id, Pos)
        SELECT [Id], ROW_NUMBER() OVER (ORDER BY ISNULL([SequenceNumber], 2147483647), [Name], [Id])
        FROM [Species]
        WHERE ISNULL([ParentId], @Empty) = ISNULL(@ParentId, @Empty);

        SELECT @OldPos = Pos FROM @Siblings WHERE Id = @SpeciesId;
        SET @NewPos = CASE WHEN @IsMovingUp = 1 THEN @OldPos - 1 ELSE @OldPos + 1 END;

        IF @OldPos IS NULL OR NOT EXISTS (SELECT 1 FROM @Siblings WHERE Pos = @NewPos)
        BEGIN
            ROLLBACK TRANSACTION;
            RAISERROR('Sequence change failed: There is no species above/below this one', 16, 1);
            RETURN;
        END

        UPDATE s
        SET s.[SequenceNumber] = n.NewPos
        FROM [Species] s
        INNER JOIN (
            SELECT Id,
                CASE WHEN Id = @SpeciesId THEN @NewPos WHEN Pos = @NewPos THEN @OldPos ELSE Pos END AS NewPos
            FROM @Siblings
        ) n ON n.Id = s.[Id]
        WHERE s.[SequenceNumber] IS NULL OR s.[SequenceNumber] <> n.NewPos;

        IF @ParentId IS NULL
            SET @ParentName = '- root species -';
        ELSE
            SELECT @ParentName = [Name] FROM [Species] WHERE [Id] = @ParentId;

        INSERT INTO [SpeciesTableAuditLog]
            ([Id], [UserId], [EffectiveDate], [LogDate], [Reason], [OldName], [NewName], [OldParent], [NewParent])
        VALUES
            (NEWID(), @UserId, GETDATE(), GETDATE(),
             CASE WHEN @IsMovingUp = 1 THEN 'Moved up within the hierarchy' ELSE 'Moved down within the hierarchy' END,
             @Name, @Name, @ParentName, @ParentName);

        COMMIT TRANSACTION;
        """;

    /// <summary>Marks a species inactive, and writes the audit trail entry.</summary>
    public const string InactivateSpecies = "sppSpecies";

    /// <summary>Deletes a species and its answer data, and writes the audit trail entry.</summary>
    public const string DeleteSpecies = "spdSpecies";
}
