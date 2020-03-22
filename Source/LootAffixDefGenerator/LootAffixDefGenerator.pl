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
use Term::ANSIColor;
use Text::CSV;
use Text::Wrap;
use XML::Twig;

$| = 1;

##################################################################################################
# Globals

$Text::Wrap::columns = 1000;

our $INPUT_CSV = file('./RimLoot Datasheet - Data.csv');
our $OUTPUT_BASE_DIR = dir('../../1.1/Defs/LootAffixDefs');

our %SUPPORTED_MODIFIERS = (qw<
    StatDefChange               1
    EquippedStatDefChange       1
    VerbPropertiesChange        1
>);

our %STAT_XML_NAME = (qw<
    StatDefChange               affectedStat
    EquippedStatDefChange       affectedStat
    VerbPropertiesChange        affectedField
>);

our $DEBUG = 3;

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
foreach my $i (0 .. $#$header_row) {
    $header_row->[$i] =~ s/[\r\n\s]//g;

    my $header = $header_row->[$i];
    $header_index{$header} //= [];
    push @{ $header_index{$header} }, $i;
}

# CSV Loop
while (my $row = $csv->getline($fh)) {
    my $modifier_class = $row->[ $header_index{LootAffixModifier}[0] ];
    my @stats          = split m<\s*[/,]\s*>, $row->[ $header_index{Stat}[0] ];
    next unless $SUPPORTED_MODIFIERS{$modifier_class};
    next unless @stats;

    my $group_name = join('_', $modifier_class, $stats[0]);

    say "$modifier_class --> ".join(' / ', @stats) if $DEBUG >= 2;

    # New XML
    my $root = XML::Twig::Elt->new('Defs');

    # Process each column section (positive, upgrade, negative)
    my $has_valid_defs = 0;
    foreach my $s (0,1,2) {
        my $raw_change_data = $row->[ $header_index{'Chance/Change'}[$s] ];
        my $affix_cost      = $row->[ $header_index{'AffixCost'}[$s]     ];
        my $def_name        = $row->[ $header_index{'Adjective'}[$s]     ];

        say "    ".join(' | ', $def_name, $raw_change_data, $affix_cost) if $DEBUG >= 3;

        next unless $raw_change_data && length $affix_cost;

        unless ($def_name) {
            print STDERR colored(['bold yellow'], "$stats[0] needs an Adjective!")."\n";
            next;
        }

        #<RimLoot.LootAffixDef>
        #    <defName>Unbreakable</defName>
        #    <groupName>StatDefChange_MaxHitPoints</groupName>
        #    <modifiers>
        #        <li Class="RimLoot.LootAffixModifier_StatDefChange">
        #            <affectedStat>MaxHitPoints</affectedStat>
        #            <valueModifier>
        #                <multiplier>4</multiplier>
        #            </valueModifier>
        #        </li>
        #        <li Class="RimLoot.LootAffixModifier_StatDefChange">
        #            <affectedStat>Flammability</affectedStat>
        #            <valueModifier>
        #                <maxValue>0</maxValue>
        #            </valueModifier>
        #        </li>
        #        <li Class="RimLoot.LootAffixModifier_StatDefChange">
        #            <affectedStat>DeteriorationRate</affectedStat>
        #            <valueModifier>
        #                <maxValue>0</maxValue>
        #            </valueModifier>
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

        my $def_xml = XML::Twig::Elt->new('RimLoot.LootAffixDef');
        $def_xml->insert_new_elt(last_child => defName   => $def_name);
        $def_xml->insert_new_elt(last_child => label     => $def_name);
        $def_xml->insert_new_elt(last_child => groupName => $group_name);

        # Parse through the change data
        my @change_values = split /\s*,\s*/, $raw_change_data;

        my $modifiers_xml = XML::Twig::Elt->new('modifiers');
        foreach my $i (0 .. $#stats) {
            my $modifier_xml = XML::Twig::Elt->new('li');
            $modifier_xml->set_atts( Class => "RimLoot.LootAffixModifier_$modifier_class" );

            $modifier_xml->insert_new_elt(last_child => $STAT_XML_NAME{$modifier_class} => $stats[$i]);

            my $value_modifier_xml = XML::Twig::Elt->new('valueModifier');
            foreach my $property (@change_values) {
                my ($elt_name, $values) = split /\s*:\s*/, $property, 2;
                my @values = split m<\s*/\s*>, $values;

                # The values are in the order of the stats (eg: A/B/C & setValue: 0/1/2 == A=0, B=1, C=2)
                if (defined $values[$i] && length $values[$i]) {
                    if ($elt_name =~ /^(?: (?:pre)?(min|max|set|add)Value | multiplier )$/ix) {
                        $value_modifier_xml->insert_new_elt(last_child => $elt_name => $values[$i]);
                    }
                    else {
                        $modifier_xml->insert_new_elt(last_child => $elt_name => $values[$i]);
                    }
                }
            }

            if ($value_modifier_xml->children_count > 0) {
                $value_modifier_xml->paste_last_child($modifier_xml);
            }
            if ($modifier_xml->children_count > 1) {
                $modifier_xml->paste_last_child($modifiers_xml);
            }
        }

        ### XXX: Account for special modifier classes in the future...
        next unless $modifiers_xml->children_count;

        $modifiers_xml->paste_last_child($def_xml);
        $def_xml->insert_new_elt(last_child => affixCost => $affix_cost);

        # Compose the affixRulePack
        my $rule_pack_xml = XML::Twig::Elt->new('affixRulePack');
        my $rules_str_xml = XML::Twig::Elt->new('rulesStrings');

        my %word_properties;
        foreach my $word_class (qw< possessive adjective noun verb >) {
            my $index = $header_index{ucfirst $word_class}[$s] // next;
            my $word  = $row->[$index] || next;

            if ($word_class eq 'noun') {
                my $is_singular = ($word =~ s/^the //i);
                $word_properties{noun_is_singular} = $is_singular ? 'True' : 'False';
            }

            $rules_str_xml->insert_new_elt(last_child => li => join('->', $word_class, $word));
        }
        $rules_str_xml->insert_new_elt(last_child => li => join('->', $_, $word_properties{$_})) for keys %word_properties;

        $rules_str_xml->paste_last_child($rule_pack_xml);
        $rule_pack_xml->paste_last_child($def_xml);

        # Attach the LootAffixDef
        $def_xml->paste_last_child($root);

        $has_valid_defs = 1;

    }

    next unless $has_valid_defs;

    # Prettify the output
    my $xml = XML::Twig->new(
        pretty_print    => 'indented_c',
        comments        => 'keep',
        output_encoding => 'UTF-8',
    );
    $xml->parse( $xml->xmldecl."\n".$root->outer_xml );

    # XML::Twig does this dumb thing with tab characters...
    my $xml_text = $xml->sprint;
    $xml_text =~ s/\t/        /g;
    $xml_text =~ s/^(\s+)/" " x (length($1) * 2)/gme;
    $xml_text =~ s<(\s+<RimLoot.LootAffixDef>)><\n$1>;  # only the first one
    $xml_text =~ s<(\s+</RimLoot.LootAffixDef>)><$1\n>g;

    # Save XML
    my $file = $OUTPUT_BASE_DIR->file( join('_', 'LootAffixDef', $group_name).".xml" );
    say "Writing XML file: $file" if $DEBUG >= 1;

    my $out = $file->open('>:encoding(UTF-8)') || die "Can't open $file for writing: $!";
    $out->print($xml_text);
    $out->close;
    $xml->purge;
}

close $fh;