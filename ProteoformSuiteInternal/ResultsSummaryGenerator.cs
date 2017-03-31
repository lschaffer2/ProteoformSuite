﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Text;

namespace ProteoformSuiteInternal
{
    public static class ResultsSummaryGenerator
    {
        public static string generate_full_report()
        {
            return
                counts() +
                proteins_of_significance() +
                go_terms_of_significance() +
                loaded_files_report();
        }

        public static string loaded_files_report()
        {
            string header = "DECONVOLUTION RESULTS FILES AND PROTEIN DATABASE FILES" + Environment.NewLine;
            string report = "";
            foreach (Purpose p in Lollipop.input_files.Select(f => f.purpose).Distinct())
            {
                report += p.ToString() + ":" + Environment.NewLine + String.Join(Environment.NewLine, Lollipop.get_files(Lollipop.input_files, p).Select(f => f.filename + f.extension + "\t" + f.complete_path)) + Environment.NewLine + Environment.NewLine;
            }
            return header + report;
        }

        public static string counts()
        {
            string report = "";

            report += Lollipop.raw_experimental_components.Count.ToString() + "\tRaw Experimental Components" + Environment.NewLine;
            report += Lollipop.raw_quantification_components.Count.ToString() + "\tRaw Quantitative Components" + Environment.NewLine;
            report += Lollipop.raw_neucode_pairs.Count.ToString() + "\tRaw NeuCode Pairs" + Environment.NewLine;
            report += Lollipop.raw_neucode_pairs.Count(nc => nc.accepted).ToString() + "\tAccepted NeuCode Pairs" + Environment.NewLine + Environment.NewLine;

            report += Lollipop.proteoform_community.experimental_proteoforms.Length.ToString() + "\tExperimental Proteoforms" + Environment.NewLine;
            report += Lollipop.proteoform_community.experimental_proteoforms.Count(e => e.accepted).ToString() + "\tAccepted Experimental Proteoforms" + Environment.NewLine;
            report += Lollipop.theoretical_proteins.Sum(kv => kv.Value.Length).ToString() + "\tTheoretical Proteins" + Environment.NewLine;
            report += Lollipop.expanded_proteins.Length + "\tExpanded Theoretical Proteins" + Environment.NewLine;
            report += Lollipop.proteoform_community.theoretical_proteoforms.Length.ToString() + "\tTheoretical Proteoforms" + Environment.NewLine + Environment.NewLine;

            report += Lollipop.et_peaks.Count.ToString() + "\tExperimental-Theoretical Peaks" + Environment.NewLine;
            report += Lollipop.et_relations.Count.ToString() + "\tExperimental-Theoretical Pairs" + Environment.NewLine;
            report += Lollipop.et_peaks.Count(p => p.peak_accepted).ToString() + "\tAccepted Experimental-Theoretical Peaks" + Environment.NewLine;
            report += Lollipop.et_relations.Count(r => r.accepted).ToString() + "\tAccepted Experimental-Theoretical Pairs" + Environment.NewLine;
            if (Lollipop.ed_relations.Count > 0) report += Lollipop.ed_relations.Average(d => d.Value.Count).ToString() + "\tAverage Experimental-Decoy Pairs" + Environment.NewLine + Environment.NewLine;
            else report += Environment.NewLine;

            report += Lollipop.ee_peaks.Count.ToString() + "\tExperimental-Experimental Peaks" + Environment.NewLine;
            report += Lollipop.ee_relations.Count.ToString() + "\tExperimental-Experimental Pairs" + Environment.NewLine;
            report += Lollipop.ee_peaks.Count(p => p.peak_accepted).ToString() + "\tAccepted Experimental-Experimental Peaks" + Environment.NewLine;
            report += Lollipop.ee_relations.Count(r => r.accepted).ToString() + "\tAccepted Experimental-Experimental Pairs" + Environment.NewLine;
            report += Lollipop.ef_relations.Count.ToString() + "\tTotal Experimental-False Pairs" + Environment.NewLine + Environment.NewLine;

            report += Lollipop.proteoform_community.families.Count.ToString() + "\tProteoform Families" + Environment.NewLine;
            List<ProteoformFamily> identified_families = Lollipop.proteoform_community.families.Where(f => f.gene_names.Select(g => g.ordered_locus).Distinct().Count() == 1).ToList();
            List<ProteoformFamily> ambiguous_families = Lollipop.proteoform_community.families.Where(f => f.gene_names.Select(g => g.ordered_locus).Distinct().Count() > 1).ToList();
            report += identified_families.Count.ToString() + "\tIdentified Families (Correspond to 1 gene)" + Environment.NewLine;
            report += identified_families.Sum(f => f.experimental_count).ToString() + "\tExperimental Proteoforms in Identified Families" + Environment.NewLine;
            report += ambiguous_families.Count.ToString() + "\tAmbiguous Families (Correspond to > 1 gene)" + Environment.NewLine;
            report += ambiguous_families.Sum(f => f.experimental_count).ToString() + "\tExperimental Proteoforms in Ambiguous Families" + Environment.NewLine;
            report += Lollipop.proteoform_community.families.Count(f => f.proteoforms.Count == 1).ToString() + "\tOrphaned Experimental Proteoforms (Not joined with another proteoform)" + Environment.NewLine + Environment.NewLine;

            report += Lollipop.satisfactoryProteoforms.Count.ToString() + "\tQuantified Experimental Proteoforms (Threshold for Quantification: " + Lollipop.minBiorepsWithObservations.ToString() + " = " + Lollipop.observation_requirement + ")" + Environment.NewLine;
            report += Lollipop.satisfactoryProteoforms.Count(p => p.quant.significant).ToString() + "\tExperimental Proteoforms with Significant Change (Threshold for Significance: Log2FoldChange > " + Lollipop.minProteoformFoldChange.ToString() + ", & Total Intensity from Quantification > " + Lollipop.minProteoformIntensity.ToString() + ", & Q-Value < " + Lollipop.minProteoformFDR.ToString() + ")" + Environment.NewLine + Environment.NewLine;

            report += Lollipop.getInterestingFamilies(Lollipop.satisfactoryProteoforms, Lollipop.minProteoformFoldChange, Lollipop.minProteoformFDR, Lollipop.minProteoformIntensity).Count.ToString() + "\tProteoform Families with Significant Change" + Environment.NewLine;
            report += Lollipop.inducedOrRepressedProteins.Count.ToString() + "\tIdentified Proteins with Significant Change" + Environment.NewLine;
            report += Lollipop.goTermNumbers.Count(g => g.by < (double)Lollipop.minProteoformFDR).ToString() + "\tGO Terms of Significance (Benjimini-Yekeulti p-value < " + Lollipop.minProteoformFDR.ToString() + "): " + Environment.NewLine + Environment.NewLine;

            return report;
       }

        public static string proteins_of_significance()
        {
            return "Identified Proteins with Significant Change: " + Environment.NewLine 
                + String.Join(Environment.NewLine, Lollipop.inducedOrRepressedProteins.Select(p => p.Accession).Distinct().OrderBy(x => x)) + Environment.NewLine + Environment.NewLine;
        }

        public static string go_terms_of_significance()
        {
            return "GO Terms of Significance (Benjimini-Yekeulti p-value < " + Lollipop.minProteoformFDR.ToString() + "): " + Environment.NewLine 
                + String.Join(Environment.NewLine, Lollipop.goTermNumbers.Where(g => g.by < (double)Lollipop.minProteoformFDR).Select(g => g.ToString()).OrderBy(x => x)) + Environment.NewLine + Environment.NewLine;

        }

        public static string results_dataframe()
        {
            DataTable results = new DataTable();
            results.Columns.Add("Proteoform ID", typeof(string));
            results.Columns.Add("Aggregated Observation ID", typeof(string));
            results.Columns.Add("SGD ID", typeof(string));
            results.Columns.Add("Gene Name", typeof(string));
            results.Columns.Add("Protein Fragment Type", typeof(string));
            results.Columns.Add("PTM Type", typeof(string));
            results.Columns.Add("Mass Difference", typeof(double));
            results.Columns.Add("Retention Time", typeof(double));
            results.Columns.Add("Aggregated Intensity", typeof(double));
            results.Columns.Add((Lollipop.numerator_condition == "" ? "Condition #1" : Lollipop.numerator_condition) + " Quantified Proteoform Intensity", typeof(double));
            results.Columns.Add((Lollipop.denominator_condition == "" ? "Condition #2" : Lollipop.denominator_condition) + " Quantified Proteoform Intensity", typeof(double));
            results.Columns.Add("Statistically Significant", typeof(bool));

            foreach (ExperimentalProteoform e in Lollipop.proteoform_community.families.SelectMany(f => f.experimental_proteoforms)
                .OrderByDescending(e => e.quant.significant ? 1 : 0)
                .ThenBy(e => e.theoretical_reference_accession)
                .ThenBy(e => e.ptm_set.ptm_combination.Count))
            {
                if (e.theoretical_reference == null) continue;

                results.Rows.Add(
                    e.theoretical_reference_accession,
                    e.accession,
                    e.theoretical_reference.gene_name.ordered_locus,
                    e.theoretical_reference.gene_name.primary,
                    e.theoretical_reference_fragment,
                    String.Join("; ", e.ptm_set.ptm_combination.Select(ptm => ptm.modification.id)),
                    e.modified_mass - e.theoretical_reference.modified_mass,
                    e.agg_rt,
                    e.agg_intensity,
                    e.quant.lightIntensitySum,
                    e.quant.heavyIntensitySum,
                    e.quant.significant
                );
            }

            StringBuilder result_string = new StringBuilder();
            string header = "";
            foreach (DataColumn column in results.Columns)
            {
                header += column.ColumnName + "\t";
            }
            result_string.AppendLine(header);
            foreach (DataRow row in results.Rows)
            {
                result_string.AppendLine(String.Join("\t", row.ItemArray));
            }
            return result_string.ToString();
        }
    }
}