#!/usr/bin/perl

use utf8;
use v5.14;
use strict;
use warnings;
use open ':std', ':encoding(utf8)';
use feature 'unicode_strings';

use Data::Dumper;
use List::Util qw< any none >;
use Path::Class;
use Text::CSV;
use Text::Wrap;
use XML::Twig;

$| = 1;

##################################################################################################
# Globals

$Text::Wrap::columns = 1000;

our $INPUT_CSV = file('./RimLoot - Data.csv');
our $OUTPUT_BASE_DIR = dir('../../1.1/Defs/LootAffixDefs');

our %SUPPORTED_MODIFIERS = (qw<
    StatDefChange    1
>);

our $DEBUG = 2;

##################################################################################################

# Open CSV
my $csv = Text::CSV->new({
    binary => 1,
    auto_diag => 1
});
open my $fh, "<:encoding(utf8)", $INPUT_CSV or die "Can't open $INPUT_CSV for writing: $!";

# First two rows are headers, but the second row is more useful
my $header_row = $csv->getline($fh);
$header_row = $csv->getline($fh);

my %header_index;
foreach my $i (0 .. $#header_row) {
    $header_row->[$i] =~ s/[\R\s]//g;

    my $header = $header_row->[$i];
    $header_index{$header} //= [];
    push @{ $header_index{$header} }, $i;
}

# CSV Loop
while (my $row = $csv->getline($fh)) {
    my $modifier_class = $row->[ $header_index{LootAffixModifiers}[0] ];
    my @stats          = split m<\s*/\s*>, $row->[ $header_index{Stat}[0] ];
    next unless $SUPPORTED_MODIFIERS{$modifier_class};
    next unless @stats;

    # New XML
    my $xml = XML::Twig->new(
        pretty_print    => 'indented_c',
        comments        => 'keep',
        output_encoding => 'UTF-8',
    );
    my $root = XML::Twig::Elt->new('Defs');
    $xml->root($root);
    $root->insert_new_elt(last_child => '#PCDATA' => "\n");

    # Process each column section (positive, upgrade, negative)
    my $has_valid_defs = 0;
    foreach my $s (0,1,2) {
        my $raw_change_data = $row->[ $header_index{'Chance/Change'}[$s] ];
        my $affix_cost      = $row->[ $header_index{'AffixCost'}[$s]     ];
        my $def_name        = $row->[ $header_index{'Adjective'}[$s]     ];
        next unless $raw_change_data && length $affix_cost;

        unless ($def_name) {
            warn "$stats[0] needs an Adjective!\n";
            next;
        }

        #<RimLoot.LootAffixDef>
        #    <defName>Unbreakable</defName>
        #    <groupName>StatDefChange_MaxHitPoints</groupName>
        #    <modifiers>
        #        <li Class="RimLoot.LootAffixModifier_StatDefChange">
        #            <affectedStat>MaxHitPoints</affectedStat>
        #            <multiplier>4</multiplier>
        #        </li>
        #        <li Class="RimLoot.LootAffixModifier_StatDefChange">
        #            <affectedStat>Flammability</affectedStat>
        #            <maxValue>0</maxValue>
        #        </li>
        #        <li Class="RimLoot.LootAffixModifier_StatDefChange">
        #            <affectedStat>DeteriorationRate</affectedStat>
        #            <maxValue>0</maxValue>
        #        </li>
        #    </modifiers>
        #    <affixCost>2</affixCost>
        #    <affixRulePack>
        #        <rulesStrings>
        #            <li>adjective->Unbreakable</li>
        #            <li>noun->Unbreakability</li>
        #            <li>verb->Everlasting</li>
        #            <li>noun_is_singular->False</li>
        #        </rulesStrings>
        #    </affixRulePack>
        #</RimLoot.LootAffixDef>

        my $group_name = join('_', $modifier_class, $stats[0]);

        my $def_xml = XML::Twig::Elt->new('RimLoot.LootAffixDef');
        $def_xml->insert_new_elt(last_child => defName   => $def_name);
        $def_xml->insert_new_elt(last_child => groupName => $group_name);

        # Parse through the change data
        my @change_values = split /\s*,\s*/, $raw_change_data;

        my $modifiers_xml = XML::Twig::Elt->new('modifiers');
        foreach my $i (0 .. $#stats) {
            my $modifier_xml = XML::Twig::Elt->new('li');
            $modifier_xml->set_atts( Class => "RimLoot.LootAffixModifier_$modifier_class" );

            ### XXX: We might use other "base" variables
            $modifier_xml->insert_new_elt(last_child => affectedStat => $stat[$i]);

            foreach my $property (@change_values) {
                my ($elt_name, $values) = split /\s*:\s*/, $property, 2;
                my @values = split m<\s*/\s*>, $values;

                # The values are in the order of the stats (eg: A/B/C & setValue: 0/1/2 == A=0, B=1, C=2)
                if (defined $values[$i] && length $values[$i]) {
                    $modifier_xml->insert_new_elt(last_child => $elt_name => $values[$i]);
                }
            }

            if ($modifier_xml->children_count > 1) {
                $modifiers_xml->paste_last_child($modifier_xml);
            }
        }

        ### XXX: Account for special modifier classes in the future...
        next unless $modifiers_xml->children_count;

        $def_xml->paste_last_child($modifiers_xml);
        $def_xml->insert_new_elt(last_child => affixCost => $affix_cost);

        # Compose the affixRulePack
        my $rule_pack_xml = XML::Twig::Elt->new('affixRulePack');
        my $rules_str_xml = XML::Twig::Elt->new('rulesStrings');

        my %word_properties;
        foreach my $word_class (qw< possessive adjective noun verb >) {
            my $word = $row->[ $header_index{ucfirst $word_class}[$s] ];
            next unless $word;

            if ($word_class eq 'noun') {
                my $is_singular = ($word =~ s/^the //i);
                $word_properties{noun_is_singular} = $is_singular ? 'True' : 'False';
            }

            $rules_str_xml->insert_new_elt(last_child => li => join('->', $word_class, $word));
        }
        $rules_str_xml->insert_new_elt(last_child => li => join('->', $_, $word_properties{$_})) for keys %word_properties;

        $rule_pack_xml->paste_last_child($rules_str_xml);
        $def_xml->paste_last_child($rule_pack_xml);

        # Attach the LootAffixDef
        $root->paste_last_child($def_xml);
        $root->insert_new_elt(last_child => '#PCDATA' => "\n");

        $has_valid_defs = 1;
    }

    next unless $has_valid_defs;

    # Save XML
    my $file = $OUTPUT_BASE_DIR->file( join('_', 'LootAffixDef', $group_name).".xml" );
    say "Writing XML file: $file" if $DEBUG >= 1;

    my $out = $file->open('>:encoding(UTF-8)') || die "Can't open $file for writing: $!";
    $xml->print($out);
    $out->close;
    $xml->purge;
}

close $fh;