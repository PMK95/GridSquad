using System.Text;

namespace GridSquad
{
    internal static class UiTooltipContentFactory
    {
        public static UiTooltipContent CreateItem(ItemInstance item, string slotName = null)
        {
            ItemDefinition definition = item?.Definition;
            if (definition == null)
            {
                return new UiTooltipContent(
                    null,
                    string.IsNullOrWhiteSpace(slotName) ? "빈 슬롯" : slotName,
                    string.Empty,
                    "장착된 아이템이 없습니다.",
                    string.Empty,
                    string.Empty);
            }

            StringBuilder details = new();
            if (!string.IsNullOrWhiteSpace(slotName))
                details.Append("슬롯  ").Append(slotName).Append('\n');
            details.Append("수량  ").Append(item.Quantity)
                .Append("\n중량  ").Append(item.TotalWeight.ToString("0.##")).Append("kg");
            switch (definition)
            {
                case WeaponDefinition weapon:
                    details.Append("\n피해  ").Append(weapon.AttackDamage)
                        .Append("\n사거리  ").Append(weapon.RangeInCells.ToString("0.#")).Append("칸")
                        .Append("\n명중률  ").Append(weapon.BaseHitChancePercent.ToString("0")).Append('%');
                    if (weapon.UsesAmmo)
                    {
                        details.Append("\n탄약  ").Append(item.MagazineAmmo)
                            .Append('/').Append(weapon.MagazineCapacity)
                            .Append(" + ").Append(item.ReserveAmmo);
                    }
                    break;
                case ArmorDefinition armor:
                    details.Append("\n방어력  ").Append(armor.Defense)
                        .Append("\n방어 횟수  ").Append(item.Durability)
                        .Append('/').Append(armor.MaximumBlockCount);
                    break;
                case AdditionalEquipmentDefinition support:
                    if (support.PassiveKind != SupportEquipmentPassiveKind.None)
                    {
                        details.Append("\n충전  ").Append(support.MaximumPassiveCharges)
                            .Append("\n재충전  ").Append(support.PassiveRechargeSeconds.ToString("0.#"))
                            .Append("초");
                    }
                    if (support.GrantedActions.Count > 0)
                    {
                        details.Append("\n부여 액션  ");
                        AppendActionNames(details, support.GrantedActions);
                    }
                    break;
            }
            if (definition is EquippableDefinition equipment
                && equipment.StatModifiers.Count > 0)
            {
                details.Append("\n\n능력치 변경");
                foreach (UnitStatModifier modifier in equipment.StatModifiers)
                {
                    if (modifier.Definition == null)
                        continue;
                    details.Append('\n')
                        .Append(modifier.Definition.DisplayName)
                        .Append("  ")
                        .Append(modifier.Definition.FormatModifier(
                            modifier.Operation,
                            modifier.Value));
                }
            }

            return new UiTooltipContent(
                definition.Icon,
                definition.DisplayName,
                GetItemCategoryLabel(definition),
                definition.Description,
                details.ToString(),
                string.Empty);
        }

        public static UiTooltipContent CreateStat(
            Combatant combatant,
            UnitStatDefinition definition)
        {
            if (definition == null)
                return default;
            UnitRuntimeStatEntry entry = default;
            combatant?.TryGetStatEntry(definition, out entry);
            float value = combatant != null
                ? combatant.GetStatValue(definition)
                : definition.DefaultValue;
            string valueText = definition.FormatValue(value);
            StringBuilder details = new();
            details.Append("기본값  ").Append(definition.FormatValue(
                entry.Definition != null ? entry.BaseValue : definition.DefaultValue));
            if (entry.Contributions != null)
            {
                foreach (UnitStatContribution contribution in entry.Contributions)
                {
                    details.Append('\n')
                        .Append(GetModifierSourceKindLabel(contribution.SourceKind))
                        .Append(" · ")
                        .Append(string.IsNullOrWhiteSpace(contribution.SourceDisplayName)
                            ? "이름 없는 출처"
                            : contribution.SourceDisplayName)
                        .Append("  ")
                        .Append(definition.FormatModifier(
                            contribution.Operation,
                            contribution.Value));
                    float remainingSeconds = combatant != null
                        ? combatant.GetStatModifierRemainingSeconds(contribution.RuntimeHandle)
                        : -1f;
                    if (remainingSeconds >= 0f)
                        details.Append(" (").Append(remainingSeconds.ToString("0.0")).Append("초)");
                }
            }
            details.Append("\n최종값  ").Append(valueText);
            if (definition.StatId == "defense")
            {
                float reduction = value > 0f ? value / (100f + value) * 100f : 0f;
                details.Append("\n예상 피해 감소  ").Append(reduction.ToString("0.#")).Append('%');
            }
            return new UiTooltipContent(
                null,
                definition.DisplayName,
                valueText,
                definition.Description,
                details.ToString(),
                string.Empty);
        }

        public static UiTooltipContent CreateTrait(UnitTraitDefinition trait)
        {
            if (trait == null)
                return default;
            StringBuilder details = new();
            foreach (UnitStatModifier modifier in trait.Modifiers)
            {
                if (modifier.Definition == null)
                    continue;
                if (details.Length > 0)
                    details.Append('\n');
                details.Append(modifier.Definition.DisplayName)
                    .Append("  ")
                    .Append(modifier.Definition.FormatModifier(
                        modifier.Operation,
                        modifier.Value));
            }
            if (trait.GrantedActions.Count > 0)
            {
                if (details.Length > 0)
                    details.Append('\n');
                details.Append("부여 액션  ");
                AppendActionNames(details, trait.GrantedActions);
            }
            return new UiTooltipContent(
                trait.Icon,
                trait.DisplayName,
                "특성",
                trait.Description,
                details.ToString(),
                string.Empty);
        }

        private static string GetItemCategoryLabel(ItemDefinition definition)
            => definition switch
            {
                WeaponDefinition => "무기",
                ArmorDefinition => "방어구",
                AdditionalEquipmentDefinition => "지원 장비",
                OffHandDefinition => "보조 장비",
                ConsumableItemDefinition => "소모품",
                _ => "아이템"
            };

        private static string GetModifierSourceKindLabel(UnitStatModifierSourceKind sourceKind)
        {
            return sourceKind switch
            {
                UnitStatModifierSourceKind.Trait => "특성",
                UnitStatModifierSourceKind.Equipment => "장비",
                UnitStatModifierSourceKind.Action => "액션",
                UnitStatModifierSourceKind.Status => "상태",
                _ => "기타"
            };
        }

        private static void AppendActionNames(
            StringBuilder builder,
            System.Collections.Generic.IReadOnlyList<CombatActionDefinition> actions)
        {
            bool appended = false;
            foreach (CombatActionDefinition action in actions)
            {
                if (action == null)
                    continue;
                if (appended)
                    builder.Append(", ");
                builder.Append(action.DisplayName);
                appended = true;
            }
        }
    }
}
