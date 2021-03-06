﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Chemistry;

namespace ProteoformSuiteInternal
{
    //Note ExperimentalProteoform is a bit of a misnomer. These are not experimental observations, but rather aggregated experimental
    //observations. Each NeuCodePair is an ExperimentalProteoform, but this class is used after accounting for missed lysines and monoisotopics.
    //However, I think this makes the programming a bit cleaner, since "Experimental-Theoretical" pairs should naturally be between
    //"ExperimentalProteoform" and "TheoreticalProteoform" objects

    public class ExperimentalProteoform : Proteoform
    {
        #region Public Field

        public IAggregatable root;

        #endregion Public Field

        #region Public Properties

        public List<IAggregatable> aggregated { get; set; } = new List<IAggregatable>();

        public List<Component> lt_verification_components { get; set; } = new List<Component>();

        public List<Component> hv_verification_components { get; set; } = new List<Component>();

        public List<Component> lt_quant_components { get; set; } = new List<Component>();

        public List<Component> hv_quant_components { get; set; } = new List<Component>();

        public List<BiorepIntensity> biorepIntensityList { get; set; } = new List<BiorepIntensity>();

        public List<BiorepTechrepIntensity> biorepTechrepIntensityList { get; set; } = new List<BiorepTechrepIntensity>();

        // public List<BiorepFractionTechrepIntensity> bftIntensityList { get; set; } = new List<BiorepFractionTechrepIntensity>();

        public QuantitativeProteoformValues quant { get; set; }

        public double agg_mass { get; set; } = 0;

        public double agg_intensity { get; set; } = 0;

        public double agg_rt { get; set; } = 0;

        public string manual_validation_id { get; set; } = "";

        public string manual_validation_verification { get; set; } = "";

        public string manual_validation_quant { get; set; } = "";

        public bool topdown_id { get; set; }

        public bool adduct { get; set; }

        public string uniprot_mods { get; set; }

        public bool novel_mods { get; set; }

        public int proteoform_level { get; set; }

        public string proteoform_level_description { get; set; } = "";

        public bool new_intact_mass_id { get; set; }

        //if ambiguous id's store here
        //proteoform: theoretical starting point; first int: begin residue; last ent: end residue; PtmSet
        public List<AmbiguousIdentification> ambiguous_identifications { get; set; } = new List<AmbiguousIdentification>();

        public List<SpectrumMatch> bottom_up_PSMs = new List<SpectrumMatch>();

        #endregion Public Properties

        #region Public Constructors

        public ExperimentalProteoform(string accession, IAggregatable root, List<IAggregatable> candidate_observations, bool is_target)
            : base(accession)
        {
            quant = new QuantitativeProteoformValues(this);
            this.root = root;
            this.aggregated.AddRange(candidate_observations.Where(p => this.includes(p, this.root)));
            this.calculate_properties();
            this.root = this.aggregated.OrderByDescending(a => a.intensity_sum).FirstOrDefault();
        }

        public ExperimentalProteoform(string accession, IAggregatable root, bool is_target)
            : base(accession)
        {
            quant = new QuantitativeProteoformValues(this);
            this.root = root;
            this.is_target = is_target;
        }

        public ExperimentalProteoform(string accession, ExperimentalProteoform temp, List<IAggregatable> candidate_observations, bool is_target, bool neucode_labeled)
            : base(accession) //this is for first mass of aggregate components. uses a temporary component
        {
            if (neucode_labeled)
            {
                NeuCodePair ncRoot = new NeuCodePair();
                ncRoot.weighted_monoisotopic_mass = temp.agg_mass;
                ncRoot.intensity_sum = temp.agg_intensity;
                ncRoot.rt_apex = temp.agg_rt;
                ncRoot.lysine_count = temp.lysine_count;

                this.root = ncRoot;
                this.aggregated.AddRange(candidate_observations.Where(p => includes(p, this.root)));
                this.calculate_properties();
                this.root = this.aggregated.OrderByDescending(a => a.intensity_sum).FirstOrDefault(); //reset root to component with max intensity
            }
            else
            {
                Component cRoot = new Component();
                cRoot.weighted_monoisotopic_mass = temp.agg_mass;
                cRoot.intensity_sum = temp.agg_intensity;
                cRoot.rt_apex = temp.agg_rt;

                this.root = cRoot;
                this.aggregated.AddRange(candidate_observations.Where(p => includes(p, this.root)));
                calculate_properties();
                this.root = this.aggregated.OrderByDescending(a => a.intensity_sum).FirstOrDefault(); //reset root to component with max intensity
            }
        }

        // COPYING CONSTRUCTOR
        public ExperimentalProteoform(ExperimentalProteoform eP)
            : base(eP.accession, eP.modified_mass, eP.lysine_count, eP.is_target)
        {
            copy_aggregate(eP);
            quant = new QuantitativeProteoformValues(this);
        }

        #endregion Public Constructors

        #region Private Methods

        private void copy_aggregate(ExperimentalProteoform e)
        {
            root = e.root;
            //doesn't copy quant on purpose
            agg_intensity = e.agg_intensity;
            agg_mass = e.agg_mass;
            modified_mass = e.modified_mass;
            agg_rt = e.agg_rt;
            lysine_count = e.lysine_count;
            is_target = e.is_target;
            family = e.family;
            aggregated = new List<IAggregatable>(e.aggregated);
            lt_quant_components = new List<Component>(e.lt_quant_components);
            lt_verification_components = new List<Component>(e.lt_verification_components);
            hv_quant_components = new List<Component>(e.hv_quant_components);
            hv_verification_components = new List<Component>(e.hv_verification_components);
            biorepIntensityList = new List<BiorepIntensity>(e.biorepIntensityList);
            manual_validation_id = e.manual_validation_id;
            manual_validation_verification = e.manual_validation_verification;
            manual_validation_quant = e.manual_validation_quant;
        }

        public string find_manual_inspection_component(IEnumerable<IAggregatable> components)
        {
            IAggregatable intense = components.OrderByDescending(c => c.intensity_sum).FirstOrDefault();
            if (intense == null)
                return "";

            ChargeState intense_cs = intense.charge_states.OrderByDescending(c => c.intensity).FirstOrDefault();
            if (intense_cs == null)
                return "";
            var component = intense as Component;
            return (component != null ? component.id + " " : "") + "File: " + intense.input_file.filename
                + "; Scan Range: " + intense.min_scan + "-" + intense.max_scan
                + "; Charge State m/z (+" + intense_cs.charge_count.ToString() + "): " + intense_cs.mz_centroid + "; RT (min): " + intense.rt_apex;
        }

        #endregion Private Methods

        #region Public Methods

        public double calculate_mass_error(TheoreticalProteoform t, PtmSet ptm_set, int begin, int end)
        {
            string sequence = t.sequence
                     .Substring(begin < t.begin ? 0 : begin - t.begin,
                     1 + end - (begin < t.begin ? t.begin : begin));
            if (begin < t.begin) sequence = "M" + sequence;
            double theoretical_mass =
                TheoreticalProteoform.CalculateProteoformMass(sequence, ptm_set.ptm_combination);
            return Math.Round(agg_mass - theoretical_mass, 4 );
        }

        public static string get_sequence(TheoreticalProteoform t, int begin, int end)
        {
            string sequence = t.sequence
                .Substring(begin < t.begin ? 0 : begin - t.begin,
                    1 + end - (begin < t.begin ? t.begin : begin));
            if (begin < t.begin) sequence = "M" + sequence;
            return sequence;
        }

        #endregion Public Methods

        #region Aggregation Public Methods

        public void aggregate()
        {
            ExperimentalProteoform temp_pf = new ExperimentalProteoform("tbd", root, new List<IAggregatable>(Sweet.lollipop.remaining_to_aggregate), true); //first pass returns temporary proteoform
            ExperimentalProteoform new_pf = new ExperimentalProteoform("tbd", temp_pf, new List<IAggregatable>(Sweet.lollipop.remaining_to_aggregate), true, Sweet.lollipop.neucode_labeled); //second pass uses temporary protoeform from first pass.
            copy_aggregate(new_pf); //doesn't copy quant on purpose
            root = temp_pf.root; //maintain the original component root
        }

        public void verify()
        {
            foreach (Component c in Sweet.lollipop.remaining_verification_components)
            {
                consider_neucode_component(c, lt_verification_components, hv_verification_components);
            }
        }

        public void assign_quantitative_components()
        {
            foreach (Component c in Sweet.lollipop.remaining_quantification_components)
            {
                consider_neucode_component(c, lt_quant_components, hv_quant_components);
            }
        }

        private void consider_neucode_component(Component c, List<Component> lt_components, List<Component> hv_components)
        {
            bool lt_includes = includes_neucode_component(c, this, true);
            bool hv_includes = Sweet.lollipop.neucode_labeled && includes_neucode_component(c, this, false);

            if (lt_includes)
                lt_components.Add(c);
            if (hv_includes)
                hv_components.Add(c);
        }

        public void calculate_properties()
        {
            //if not neucode labeled, the intensity sum of overlapping charge states was calculated with all charge states.
            agg_intensity = aggregated.Sum(c => c.intensity_sum);
            agg_mass = aggregated.Sum(c => (c.weighted_monoisotopic_mass - Math.Round(c.weighted_monoisotopic_mass - root.weighted_monoisotopic_mass, 0) * Lollipop.MONOISOTOPIC_UNIT_MASS) * c.intensity_sum / agg_intensity); //remove the monoisotopic errors before aggregating masses
            agg_rt = aggregated.Sum(c => c.rt_apex * c.intensity_sum / agg_intensity);
            lysine_count = root as NeuCodePair != null ? (root as NeuCodePair).lysine_count : lysine_count;
            modified_mass = agg_mass;
        }

        //This aggregates based on lysine count, mass, and retention time all at the same time. Note that in the past we aggregated based on
        //lysine count first, and then aggregated based on mass and retention time afterwards, which may give a slightly different root for the
        //experimental proteoform because the precursor aggregation may shuffle the intensity order slightly. We haven't observed any negative
        //impact of this difference as of 160812. -AC
        public bool includes(IAggregatable candidate, IAggregatable root)
        {
            return tolerable_rt(candidate, root.rt_apex) && tolerable_mass(candidate.weighted_monoisotopic_mass, root.weighted_monoisotopic_mass)
                && (candidate as NeuCodePair == null || tolerable_lysCt(candidate as NeuCodePair, (root as NeuCodePair).lysine_count));
        }

        public bool includes_neucode_component(Component candidate, ExperimentalProteoform root, bool light)
        {
            double lt_corrected_mass = root.agg_mass;
            double hv_corrected_mass = root.agg_mass + root.lysine_count * Lollipop.NEUCODE_LYSINE_MASS_SHIFT;
            return tolerable_rt(candidate, root.agg_rt) && tolerable_neucode_mass(candidate, lt_corrected_mass, hv_corrected_mass, light);
        }

  
        #endregion Aggregation Public Methods

        #region Aggregation Private Methods

        private bool tolerable_rt(IAggregatable candidate, double rt_apex)
        {
            return candidate.rt_apex >= rt_apex - Sweet.lollipop.retention_time_tolerance &&
                candidate.rt_apex <= rt_apex + Sweet.lollipop.retention_time_tolerance;
        }

        private bool tolerable_lysCt(NeuCodePair candidate, int lysine_count)
        {
            List<int> acceptable_lysineCts = Enumerable.Range(lysine_count - Sweet.lollipop.maximum_missed_lysines, Sweet.lollipop.maximum_missed_lysines * 2 + 1).ToList();
            return acceptable_lysineCts.Contains(candidate.lysine_count);
        }

        private bool tolerable_mass(double candidate_mass, double corrected_mass)
        {
            foreach (int missed_mono_count in Sweet.lollipop.missed_monoisotopics_range)
            {
                double shift = missed_mono_count * Lollipop.MONOISOTOPIC_UNIT_MASS;
                double shifted_mass = corrected_mass + shift;
                double mass_tolerance = shifted_mass / 1000000 * Sweet.lollipop.mass_tolerance;
                double low = shifted_mass - mass_tolerance;
                double high = shifted_mass + mass_tolerance;
                bool tolerable_mass = candidate_mass >= low && candidate_mass <= high;
                if (tolerable_mass)
                    return true; //Return a true result immediately; acts as an OR between these conditions
            }
            return false;
        }

        private bool tolerable_neucode_mass(Component candidate, double lt_corrected_mass, double hv_corrected_mass, bool light)
        {
            foreach (int missed_mono_count in Sweet.lollipop.missed_monoisotopics_range)
            {
                double shift = missed_mono_count * Lollipop.MONOISOTOPIC_UNIT_MASS;

                double lt_shifted_mass = lt_corrected_mass + shift;
                double lt_mass_tolerance = lt_shifted_mass / 1000000d * Sweet.lollipop.mass_tolerance;
                double lt_low = lt_shifted_mass - lt_mass_tolerance;
                double lt_high = lt_shifted_mass + lt_mass_tolerance;

                double hv_shifted_mass = hv_corrected_mass + shift;
                double hv_mass_tolerance = hv_shifted_mass / 1000000d * Sweet.lollipop.mass_tolerance;
                double hv_low = hv_shifted_mass - hv_mass_tolerance;
                double hv_high = hv_shifted_mass + hv_mass_tolerance;

                if (Sweet.lollipop.neucode_labeled && lt_high > hv_low)
                {
                    double center = (lt_high + hv_low) / 2d;
                    lt_high = center;
                    hv_low = center;
                }

                bool lt_tolerable_mass = candidate.weighted_monoisotopic_mass >= lt_low && candidate.weighted_monoisotopic_mass < lt_high;
                bool hv_tolerable_mass = candidate.weighted_monoisotopic_mass >= hv_low && candidate.weighted_monoisotopic_mass < hv_high;
                if (lt_tolerable_mass && light || hv_tolerable_mass && !light)
                    return true; //Return a true result immediately; acts as an OR between these conditions
            }
            return false;
        }

        #endregion Aggregation Private Methods

        #region Quantitation Public Method

        public List<BiorepIntensity> make_biorepIntensityList<T>(List<T> lt_quant_components, List<T> hv_quant_components, IEnumerable<string> ltConditionStrings, IEnumerable<string> hvConditionStrings)
            where T : IFileIntensity
        {
            quant = new QuantitativeProteoformValues(this); //Reset quantitation if starting over from biorep requirements

            List<BiorepIntensity> biorepIntensityList = new List<BiorepIntensity>();
            List<BiorepTechrepIntensity> biotechIntensityList = new List<BiorepTechrepIntensity>();

            foreach (string condition in ltConditionStrings.Concat(hvConditionStrings).Distinct().ToList())
            {
                List<T> quants_from_condition = lt_quant_components.Where(c => c.input_file.lt_condition == condition).Concat(hv_quant_components.Where(c => c.input_file.hv_condition == condition)).ToList();
                List<InputFile> files = quants_from_condition.Select(x => x.input_file).ToList();
                List<string> bioreps = quants_from_condition.Select(c => c.input_file.biological_replicate).Distinct().ToList();
                biorepIntensityList.AddRange(bioreps.Select(b => new BiorepIntensity(false, b, condition, quants_from_condition.Where(c => c.input_file.biological_replicate == b).Sum(i => i.intensity_sum))));
                List<Tuple<string, string>> biotechs = quants_from_condition.Select(c => new Tuple<string, string>(c.input_file.biological_replicate, c.input_file.technical_replicate)).Distinct().ToList();
                biotechIntensityList.AddRange(biotechs.Select(x => new BiorepTechrepIntensity(false, x.Item1, condition, x.Item2, quants_from_condition.Where(c => c.input_file.biological_replicate == x.Item1 && c.input_file.technical_replicate == x.Item2).Sum(asdf => asdf.intensity_sum))));
            }

            this.biorepIntensityList = biorepIntensityList;
            this.biorepTechrepIntensityList = biotechIntensityList;
            return biorepIntensityList;
        }

        #endregion Quantitation Public Method

        #region Public Methods

        public void shift_masses(int shift, bool neucode_labeled)
        {
            if (!topdown_id)
            {
                foreach (IAggregatable c in aggregated)
                {
                    if (neucode_labeled)
                    {
                        if ((c as NeuCodePair).neuCodeLight.manual_mass_shift != 0) continue;
                        (c as NeuCodePair).neuCodeLight.manual_mass_shift += shift * Lollipop.MONOISOTOPIC_UNIT_MASS;
                        (c as NeuCodePair).neuCodeHeavy.manual_mass_shift += shift * Lollipop.MONOISOTOPIC_UNIT_MASS;
                    }
                    else
                    {
                        if ((c as Component).manual_mass_shift != 0) continue;
                        (c as Component).manual_mass_shift += shift * Lollipop.MONOISOTOPIC_UNIT_MASS;
                    }
                }
            }
        }

        #endregion Public Methods
    }
}