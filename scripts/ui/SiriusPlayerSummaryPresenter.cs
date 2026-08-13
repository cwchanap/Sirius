using Godot;
using System;

public static class SiriusPlayerSummaryPresenter
{
    public static void Apply(
        ExplorationHudPlayerState state,
        Label nameLabel,
        Label levelLabel,
        SiriusStatBar healthBar,
        SiriusStatBar manaBar,
        ProgressBar experienceBar)
    {
        nameLabel.Text = string.IsNullOrWhiteSpace(state.Name)
            ? "Adventurer"
            : state.Name;
        levelLabel.Text = $"Lv {state.Level}";

        healthBar.Current = state.CurrentHealth;
        healthBar.Maximum = state.MaxHealth;

        manaBar.Visible = state.MaxMana > 0;
        if (manaBar.Visible)
        {
            manaBar.Current = state.CurrentMana;
            manaBar.Maximum = state.MaxMana;
        }

        experienceBar.Visible = state.ExperienceToNext > 0;
        if (experienceBar.Visible)
        {
            experienceBar.MaxValue = state.ExperienceToNext;
            experienceBar.Value = Math.Clamp(
                state.Experience,
                0,
                state.ExperienceToNext);
        }
    }
}
