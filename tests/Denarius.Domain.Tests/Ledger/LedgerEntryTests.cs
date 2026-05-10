using Denarius.CrossCutting.Errors;
using Denarius.Domain.Ledger;
using Denarius.Domain.Ledger.Events;
using Denarius.Domain.Ledger.ValueObjects;
using FluentAssertions;

namespace Denarius.Domain.Tests.Ledger;

public sealed class LedgerEntryTests
{
    // ── Helper ─────────────────────────────────────────────────────────────────

    private static LedgerEntry CreateOpenEntry()
    {
        Money balance = Money.Create(0, Currency.BRL).Value!;
        return LedgerEntry.Open(AccountId.New(), balance, DateTimeOffset.UtcNow).Value!;
    }

    // ── Open ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Open_WhenInitialBalanceIsNegative_ShouldReturnFailure()
    {
        // Arrange — produce a negative amount by negating a positive Money
        Money negativeBalance = Money.Create(100, Currency.BRL).Value!.Negate();

        // Act
        var result = LedgerEntry.Open(AccountId.New(), negativeBalance, DateTimeOffset.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainError.NegativeBalance);
    }

    [Fact]
    public void Open_WhenInitialBalanceIsZero_ShouldSucceed()
    {
        // Arrange
        Money zeroBalance = Money.Create(0, Currency.BRL).Value!;

        // Act
        var result = LedgerEntry.Open(AccountId.New(), zeroBalance, DateTimeOffset.UtcNow);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Open_WhenInitialBalanceIsPositive_ShouldSucceed()
    {
        // Arrange
        Money positiveBalance = Money.Create(500, Currency.USD).Value!;

        // Act
        var result = LedgerEntry.Open(AccountId.New(), positiveBalance, DateTimeOffset.UtcNow);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Open_WhenSuccessful_ShouldRaiseLedgerEntryOpened()
    {
        // Arrange
        var accountId = AccountId.New();
        Money balance = Money.Create(100, Currency.BRL).Value!;

        // Act
        var result = LedgerEntry.Open(accountId, balance, DateTimeOffset.UtcNow);

        // Assert
        var evt = result.Value!.DomainEvents.OfType<LedgerEntryOpened>().Single();
        evt.AccountId.Should().Be(accountId);
        evt.InitialBalance.Should().Be(balance);
    }

    // ── RecordCredit ───────────────────────────────────────────────────────────

    [Fact]
    public void RecordCredit_WhenAmountIsPositive_ShouldRaiseCreditRecorded()
    {
        // Arrange
        LedgerEntry entry = CreateOpenEntry();
        entry.ClearDomainEvents();
        Money amount = Money.Create(200, Currency.BRL).Value!;
        Description description = Description.Create("Salary").Value!;

        // Act
        entry.RecordCredit(amount, description, DateTimeOffset.UtcNow);

        // Assert
        var evt = entry.DomainEvents.OfType<CreditRecorded>().Single();
        evt.Amount.Should().Be(amount);
    }

    [Fact]
    public void RecordCredit_WhenAmountIsZero_ShouldThrowDomainException()
    {
        // Arrange
        LedgerEntry entry = CreateOpenEntry();
        Money amount = Money.Create(0, Currency.BRL).Value!;
        Description description = Description.Create("Test").Value!;

        // Act
        Action act = () => entry.RecordCredit(amount, description, DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<DomainException>()
            .Which.Error.Should().Be(DomainError.AmountMustBePositive);
    }

    [Fact]
    public void RecordCredit_WhenAmountIsNegative_ShouldThrowDomainException()
    {
        // Arrange
        LedgerEntry entry = CreateOpenEntry();
        Money amount = Money.Create(100, Currency.BRL).Value!.Negate();
        Description description = Description.Create("Test").Value!;

        // Act
        Action act = () => entry.RecordCredit(amount, description, DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<DomainException>()
            .Which.Error.Should().Be(DomainError.AmountMustBePositive);
    }

    [Fact]
    public void RecordCredit_WhenLedgerIsClosed_ShouldThrowDomainException()
    {
        // Arrange
        LedgerEntry entry = CreateOpenEntry();
        entry.Close(DateTimeOffset.UtcNow);
        Money amount = Money.Create(50, Currency.BRL).Value!;
        Description description = Description.Create("Late entry").Value!;

        // Act
        Action act = () => entry.RecordCredit(amount, description, DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<DomainException>()
            .Which.Error.Should().Be(DomainError.LedgerClosed);
    }

    [Fact]
    public void RecordCredit_ShouldAddLineWithPositiveAmount()
    {
        // Arrange
        LedgerEntry entry = CreateOpenEntry();
        Money amount = Money.Create(300, Currency.BRL).Value!;
        Description description = Description.Create("Consulting fee").Value!;

        // Act
        entry.RecordCredit(amount, description, DateTimeOffset.UtcNow);

        // Assert — observable through Lines (internal, visible via InternalsVisibleTo)
        entry.Lines.Should().ContainSingle()
            .Which.Amount.Amount.Should().BePositive();
    }

    // ── ApplyDebit ─────────────────────────────────────────────────────────────

    [Fact]
    public void ApplyDebit_WhenAmountIsPositive_ShouldRaiseDebitApplied()
    {
        // Arrange
        LedgerEntry entry = CreateOpenEntry();
        entry.ClearDomainEvents();
        Money amount = Money.Create(100, Currency.BRL).Value!;
        Description description = Description.Create("Rent").Value!;

        // Act
        entry.ApplyDebit(amount, description, DateTimeOffset.UtcNow);

        // Assert
        var evt = entry.DomainEvents.OfType<DebitApplied>().Single();
        evt.Amount.Should().Be(amount);
    }

    [Fact]
    public void ApplyDebit_WhenLedgerIsClosed_ShouldThrowDomainException()
    {
        // Arrange
        LedgerEntry entry = CreateOpenEntry();
        entry.Close(DateTimeOffset.UtcNow);
        Money amount = Money.Create(50, Currency.BRL).Value!;
        Description description = Description.Create("Expense").Value!;

        // Act
        Action act = () => entry.ApplyDebit(amount, description, DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<DomainException>()
            .Which.Error.Should().Be(DomainError.LedgerClosed);
    }

    [Fact]
    public void ApplyDebit_ShouldAddLineWithNegativeAmount()
    {
        // Arrange
        LedgerEntry entry = CreateOpenEntry();
        Money amount = Money.Create(150, Currency.BRL).Value!;
        Description description = Description.Create("Insurance").Value!;

        // Act
        entry.ApplyDebit(amount, description, DateTimeOffset.UtcNow);

        // Assert — observable through Lines (internal, visible via InternalsVisibleTo)
        entry.Lines.Should().ContainSingle()
            .Which.Amount.Amount.Should().BeNegative();
    }

    // ── Close ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Close_ShouldSetStatusToClosed()
    {
        // Arrange
        LedgerEntry entry = CreateOpenEntry();

        // Act
        entry.Close(DateTimeOffset.UtcNow);

        // Assert
        entry.Status.Should().Be(LedgerStatus.Closed);
    }

    [Fact]
    public void Close_ShouldRaiseLedgerEntryClosed()
    {
        // Arrange
        LedgerEntry entry = CreateOpenEntry();
        entry.ClearDomainEvents();
        DateTimeOffset closedOn = DateTimeOffset.UtcNow;

        // Act
        entry.Close(closedOn);

        // Assert
        var evt = entry.DomainEvents.OfType<LedgerEntryClosed>().Single();
        evt.EntryId.Should().Be(entry.Id);
        evt.ClosedOn.Should().Be(closedOn);
    }

    [Fact]
    public void Close_WhenAlreadyClosed_ShouldThrowDomainException()
    {
        // Arrange
        LedgerEntry entry = CreateOpenEntry();
        entry.Close(DateTimeOffset.UtcNow);

        // Act
        Action act = () => entry.Close(DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<DomainException>()
            .Which.Error.Should().Be(DomainError.LedgerClosed);
    }

    // ── Money ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Money_Create_WhenAmountIsNegative_ShouldReturnFailure()
    {
        // Act
        var result = Money.Create(-1m, Currency.BRL);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainError.InvalidAmount);
    }

    [Fact]
    public void Money_Add_WhenCurrenciesDiffer_ShouldThrowDomainException()
    {
        // Arrange
        Money brl = Money.Create(100, Currency.BRL).Value!;
        Money usd = Money.Create(50, Currency.USD).Value!;

        // Act
        Action act = () => brl.Add(usd);

        // Assert
        act.Should().Throw<DomainException>()
            .Which.Error.Should().Be(DomainError.CurrencyMismatch);
    }

    [Fact]
    public void Money_Add_WhenCurrenciesMatch_ShouldReturnSum()
    {
        // Arrange
        Money first = Money.Create(100, Currency.BRL).Value!;
        Money second = Money.Create(50, Currency.BRL).Value!;

        // Act
        Money sum = first.Add(second);

        // Assert
        sum.Amount.Should().Be(150m);
        sum.Currency.Should().Be(Currency.BRL);
    }
}
