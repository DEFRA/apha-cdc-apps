using CDC.Web.Models;

namespace CDC.Web.Tests.Models;

public class AccordionQuestionViewTests
{
    [Fact]
    public void AccordionFieldOptionView_HoldsTextAndCheckedState()
    {
        var option = new AccordionFieldOptionView("Option A", true);

        Assert.Equal("Option A", option.Text);
        Assert.True(option.IsChecked);
    }

    [Fact]
    public void AccordionFieldView_HoldsLabelValueAndOptions()
    {
        IReadOnlyList<AccordionFieldOptionView> options = [new AccordionFieldOptionView("Option A", true)];

        var field = new AccordionFieldView("Field label", "Answer", IsHtml: false, options);

        Assert.Equal("Field label", field.Label);
        Assert.Equal("Answer", field.ValueDisplay);
        Assert.False(field.IsHtml);
        Assert.Same(options, field.Options);
    }

    [Fact]
    public void AccordionQuestionView_HoldsNumberTextAndFields()
    {
        IReadOnlyList<AccordionFieldView> fields = [new AccordionFieldView("Field label", "Answer", false, [])];

        var question = new AccordionQuestionView("2.1", "Question text", fields);

        Assert.Equal("2.1", question.Number);
        Assert.Equal("Question text", question.Text);
        Assert.Same(fields, question.Fields);
    }

    [Fact]
    public void QuestionAccordionViewModel_HoldsIdPrefixEmptyMessageAndQuestions()
    {
        IReadOnlyList<AccordionQuestionView> questions = [new AccordionQuestionView("2.1", "Question text", [])];

        var viewModel = new QuestionAccordionViewModel("species", "No questions found.", questions);

        Assert.Equal("species", viewModel.IdPrefix);
        Assert.Equal("No questions found.", viewModel.EmptyMessage);
        Assert.Same(questions, viewModel.Questions);
    }
}
