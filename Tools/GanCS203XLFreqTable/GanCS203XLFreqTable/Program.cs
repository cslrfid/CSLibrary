using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace GanCS203XLFreqTable
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // ex10_default_region_name.c
            for (int i = 0; i < FrequencyBand_CS710S.frequencySet.Count; i++)
            {
                Console.WriteLine("\tREGION_" + FrequencyBand_CS710S.frequencySet[i].name.ToUpper() + ",");
            }

            // ex10_regulatory.h
            for (int i = 0; i < FrequencyBand_CS710S.frequencySet.Count; i++)
            {
                Console.WriteLine("{ .name = \""
                                    + FrequencyBand_CS710S.frequencySet[i].name.ToUpper()
                                    + "\", .region_id = "
                                    + "REGION_"
                                    + FrequencyBand_CS710S.frequencySet[i].name.ToUpper()
                                    + ", },");
            }

            // ex10_regulatory_xxx.c
            for (int i = 0; i < FrequencyBand_CS710S.frequencySet.Count; i++)
            {
                //                String Format = "#include \"ex10_api/ex10_macros.h\"\r\n#include \"ex10_regulatory/ex10_regulatory_region.h\"\r\n\r\n#include <stdlib.h>\r\n#include <string.h>\r\n\r\nstatic const struct Ex10Region region = {\r\n    .region_id = {0},\r\n    .regulatory_timers =\r\n        {\r\n            .nominal_ms          = 200,\r\n            .extended_ms         = 380,\r\n            .regulatory_ms       = 400,\r\n            .off_same_channel_ms = 0,\r\n        },\r\n    .regulatory_channels =\r\n        {\r\n            .start_freq_khz = {1},\r\n            .spacing_khz    = {2},\r\n            .count          = {3},\r\n            .usable         = NULL,\r\n            .usable_count   = 0u,\r\n            .random_hop     = true,\r\n        },\r\n    .pll_divider    = 24,\r\n    .rf_filter      = UPPER_BAND,\r\n    .max_power_cdbm = 3000,\r\n};\r\n\r\nstatic const struct Ex10Region* region_ptr = &region;\r\n\r\nstatic void set_region(struct Ex10Region const* region_to_use)\r\n{\r\n    region_ptr = (region_to_use == NULL) ? &region : region_to_use;\r\n}\r\n\r\nstatic struct Ex10Region const* get_region(void)\r\n{\r\n    return region_ptr;\r\n}\r\n\r\nstatic void get_regulatory_timers(channel_index_t              channel,\r\n                                  uint32_t                     time_ms,\r\n                                  struct Ex10RegulatoryTimers* timers)\r\n{\r\n    (void)channel;\r\n    (void)time_ms;\r\n    *timers = region_ptr->regulatory_timers;\r\n}\r\n\r\nstatic void regulatory_timer_set_start(channel_index_t channel,\r\n                                       uint32_t        time_ms)\r\n{\r\n    (void)channel;\r\n    (void)time_ms;\r\n}\r\n\r\nstatic void regulatory_timer_set_end(channel_index_t channel, uint32_t time_ms)\r\n{\r\n    (void)channel;\r\n    (void)time_ms;\r\n}\r\n\r\nstatic void regulatory_timer_clear(void) {}\r\n\r\nstatic struct Ex10RegionRegulatory const ex10_default_regulatory = {\r\n    .set_region                 = set_region,\r\n    .get_region                 = get_region,\r\n    .get_regulatory_timers      = get_regulatory_timers,\r\n    .regulatory_timer_set_start = regulatory_timer_set_start,\r\n    .regulatory_timer_set_end   = regulatory_timer_set_end,\r\n    .regulatory_timer_clear     = regulatory_timer_clear,\r\n};\r\n\r\nstruct Ex10RegionRegulatory const* get_ex10_fcc_regulatory(void)\r\n{\r\n    return &ex10_default_regulatory;\r\n}";
                //String Format1 = "#include \"ex10_api/ex10_macros.h\"\r\n#include \"ex10_regulatory/ex10_regulatory_region.h\"\r\n\r\n#include <stdlib.h>\r\n#include <string.h>\r\n\r\nstatic const struct Ex10Region region = {\r\n    .region_id = {0},\r\n    .regulatory_timers =\r\n        {\r\n            .nominal_ms          = 200,\r\n            .extended_ms         = 380,\r\n            .regulatory_ms       = 400,\r\n            .off_same_channel_ms = 0,\r\n        },\r\n    .regulatory_channels =\r\n        {\r\n            .start_freq_khz = {1},\r\n            .spacing_khz    = {2},\r\n            .count          = {3},\r\n            .usable         = NULL,\r\n            .usable_count   = 0u,\r\n            .random_hop     = true,\r\n        },\r\n    .pll_divider    = 24,\r\n    .rf_filter      = UPPER_BAND,\r\n    .max_power_cdbm = 3000,\r\n};\r\n\r\nstatic const struct Ex10Region* region_ptr = &region;\r\n\r\nstatic void set_region(struct Ex10Region const* region_to_use)\r\n{\r\n    region_ptr = (region_to_use == NULL) ? &region : region_to_use;\r\n}\r\n\r\nstatic struct Ex10Region const* get_region(void)\r\n{\r\n    return region_ptr;\r\n}\r\n\r\nstatic void get_regulatory_timers(channel_index_t              channel,\r\n                                  uint32_t                     time_ms,\r\n                                  struct Ex10RegulatoryTimers* timers)\r\n{\r\n    (void)channel;\r\n    (void)time_ms;\r\n    *timers = region_ptr->regulatory_timers;\r\n}\r\n\r\nstatic void regulatory_timer_set_start(channel_index_t channel,\r\n                                       uint32_t        time_ms)\r\n{\r\n    (void)channel;\r\n    (void)time_ms;\r\n}\r\n\r\nstatic void regulatory_timer_set_end(channel_index_t channel, uint32_t time_ms)\r\n{\r\n    (void)channel;\r\n    (void)time_ms;\r\n}\r\n\r\nstatic void regulatory_timer_clear(void) {}\r\n\r\nstatic struct Ex10RegionRegulatory const ex10_default_regulatory = {\r\n    .set_region                 = set_region,\r\n    .get_region                 = get_region,\r\n    .get_regulatory_timers      = get_regulatory_timers,\r\n    .regulatory_timer_set_start = regulatory_timer_set_start,\r\n    .regulatory_timer_set_end   = regulatory_timer_set_end,\r\n    .regulatory_timer_clear     = regulatory_timer_clear,\r\n};\r\n\r\nstruct Ex10RegionRegulatory const* get_ex10_fcc_regulatory(void)\r\n{\r\n    return &ex10_default_regulatory;\r\n}";

                String Format;

                Format = "#include \"ex10_api/ex10_macros.h\"\r\n#include \"ex10_regulatory/ex10_regulatory_region.h\"\r\n\r\n#include <stdlib.h>\r\n#include <string.h>\r\n\r\nstatic const struct Ex10Region region = {\r\n    .region_id = ";
                Console.Write(Format + "REGION_" + FrequencyBand_CS710S.frequencySet[i].name.ToUpper());

                Format = ",\r\n    .regulatory_timers =\r\n        {\r\n            .nominal_ms          = 200,\r\n            .extended_ms         = 380,\r\n            .regulatory_ms       = 400,\r\n            .off_same_channel_ms = 0,\r\n        },\r\n    .regulatory_channels =\r\n        {\r\n            .start_freq_khz = ";
                Console.Write(Format + FrequencyBand_CS710S.frequencySet[i].firstChannel.ToString());

                Format = ",\r\n            .spacing_khz    = ";
                Console.Write(Format + FrequencyBand_CS710S.frequencySet[i].channelSepatration.ToString());

                Format = ",\r\n            .count          = ";
                Console.Write(Format + FrequencyBand_CS710S.frequencySet[i].totalFrequencyChannel.ToString());

                Format = ",\r\n            .usable         = NULL,\r\n            .usable_count   = 0u,\r\n            .random_hop     = ";
                Console.Write(Format + (FrequencyBand_CS710S.frequencySet[i].hopping == "Hop" ? "true" : "false"));

                Format = ",\r\n        },\r\n    .pll_divider    = 24,\r\n    .rf_filter      = UPPER_BAND,\r\n    .max_power_cdbm = 3000,\r\n};\r\n\r\nstatic const struct Ex10Region* region_ptr = &region;\r\n\r\nstatic void set_region(struct Ex10Region const* region_to_use)\r\n{\r\n    region_ptr = (region_to_use == NULL) ? &region : region_to_use;\r\n}\r\n\r\nstatic struct Ex10Region const* get_region(void)\r\n{\r\n    return region_ptr;\r\n}\r\n\r\nstatic void get_regulatory_timers(channel_index_t              channel,\r\n                                  uint32_t                     time_ms,\r\n                                  struct Ex10RegulatoryTimers* timers)\r\n{\r\n    (void)channel;\r\n    (void)time_ms;\r\n    *timers = region_ptr->regulatory_timers;\r\n}\r\n\r\nstatic void regulatory_timer_set_start(channel_index_t channel,\r\n                                       uint32_t        time_ms)\r\n{\r\n    (void)channel;\r\n    (void)time_ms;\r\n}\r\n\r\nstatic void regulatory_timer_set_end(channel_index_t channel, uint32_t time_ms)\r\n{\r\n    (void)channel;\r\n    (void)time_ms;\r\n}\r\n\r\nstatic void regulatory_timer_clear(void) {}\r\n\r\nstatic struct Ex10RegionRegulatory const ex10_default_regulatory = {\r\n    .set_region                 = set_region,\r\n    .get_region                 = get_region,\r\n    .get_regulatory_timers      = get_regulatory_timers,\r\n    .regulatory_timer_set_start = regulatory_timer_set_start,\r\n    .regulatory_timer_set_end   = regulatory_timer_set_end,\r\n    .regulatory_timer_clear     = regulatory_timer_clear,\r\n};\r\n\r\nstruct Ex10RegionRegulatory const* ";
                Console.Write(Format + "get_ex10_" + FrequencyBand_CS710S.frequencySet[i].name.ToLower() + "_regulatory");

                Format = "(void)\r\n{\r\n    return &ex10_default_regulatory;\r\n}";
                Console.Write(Format);
            }
            Console.WriteLine();

            // ex10_regulator.c
            for (int i = 0; i < FrequencyBand_CS710S.frequencySet.Count; i++)
            {
                Console.WriteLine("case {0}:\r\n                    return {1}();",
                "REGION_" + FrequencyBand_CS710S.frequencySet[i].name.ToUpper(),
                "get_ex10_" + FrequencyBand_CS710S.frequencySet[i].name.ToLower() + "_regulatory"
                    );
            }
        }
    }
}

public static class FrequencyBand_CS710S
{
    public class FREQUENCYSET
    {
        public int index;                  // CSL E710 Country Enum
        public string name;                // CSL E710 Country Name
        public string modelCode;           // CSL Reader Model Code(Region Code)
        public int totalFrequencyChannel;  // Frequency Channel #
        public string hopping;             // Fixed or Hop
        public int onTime;                 // Hop Time or On Time
        public int offTime;                // Off Time
        public int channelSepatration;     // Channel separation
        public double firstChannel;        // First Channel
        public double lastChannel;         // Last Channel
        public string note;                // Note

        public FREQUENCYSET(int index, string name, string modelCode, int totalFrequencyChannel, string hopping, int onTime, int offTime, int channelSepatration, double firstChannel, double lastChannel, string note)
        {
            this.index = index;
            this.name = name;
            this.modelCode = modelCode;
            this.totalFrequencyChannel = totalFrequencyChannel;
            this.hopping = hopping;
            this.onTime = onTime;
            this.offTime = offTime;
            this.channelSepatration = channelSepatration;
            this.firstChannel = firstChannel;
            this.lastChannel = lastChannel;
            this.note = note;
        }
    }

    public static List<FREQUENCYSET> frequencySet;

    static FrequencyBand_CS710S()
    {
        frequencySet = new List<FREQUENCYSET>();

        frequencySet.Add(new FREQUENCYSET(0, "UNKNOW", "", 0, "", 0, 0, 0, 0, 0, ""));
        frequencySet.Add(new FREQUENCYSET(1, "Albania1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(2, "Albania2", "-2 RW", 23, "Hop", 400, -1, 250, 915.25, 920.75, "915-921"));
        frequencySet.Add(new FREQUENCYSET(3, "Algeria1", "-1", 4, "Fixed", 3900, 100, 600, 871.6, 873.4, "870-876"));
        frequencySet.Add(new FREQUENCYSET(4, "Algeria2", "-1", 4, "Fixed", 3900, 100, 600, 881.6, 883.4, "880-885"));
        frequencySet.Add(new FREQUENCYSET(5, "Algeria3", "-9", 3, "Fixed", 3900, -1, 1200, 916.3, 918.7, "915-921"));
        frequencySet.Add(new FREQUENCYSET(6, "Algeria4", "-7", 2, "Fixed", 3900, 100, 500, 925.25, 925.75, "925-926"));
        frequencySet.Add(new FREQUENCYSET(7, "Argentina", "-2 RW", 50, "Hop", 400, -1, 500, 902.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(8, "Armenia", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(9, "Australia1", "-2 AS", 10, "Hop", 400, -1, 500, 920.75, 925.25, ""));
        frequencySet.Add(new FREQUENCYSET(10, "Australia2", "-2 AS", 14, "Hop", 400, -1, 500, 918.75, 925.25, ""));
        frequencySet.Add(new FREQUENCYSET(11, "Austria1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(12, "Austria2", "-9", 3, "Fixed", 3900, -1, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(13, "Azerbaijan", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(14, "Bahrain", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(15, "Bangladesh", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(16, "Belarus", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(17, "Belgium1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(18, "Belgium2", "-9", 3, "Fixed", 3900, -1, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(19, "Bolivia", "-2", 50, "Hop", 400, -1, 500, 902.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(20, "Bosnia", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(21, "Botswana", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(22, "Brazil1", "-2 RW", 9, "Fixed", 3900, 100, 500, 902.75, 906.75, ""));
        frequencySet.Add(new FREQUENCYSET(23, "Brazil2", "-2 RW", 24, "Fixed", 3900, 100, 500, 915.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(24, "Brunei1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(25, "Brunei2", "-7", 7, "Fixed", 3900, 100, 250, 923.25, 924.75, "923 - 925"));
        frequencySet.Add(new FREQUENCYSET(26, "Bulgaria1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(27, "Bulgaria2", "-9", 3, "Fixed", 3900, -1, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(28, "Cambodia", "-7", 16, "Hop", 400, -1, 250, 920.625, 924.375, ""));
        frequencySet.Add(new FREQUENCYSET(29, "Cameroon", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(30, "Canada", "-2", 50, "Hop", 400, -1, 500, 902.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(31, "Chile1", "-2 RW", 3, "Fixed", 3900, 100, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(32, "Chile2", "-2 RW", 24, "Hop", 400, -1, 500, 915.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(33, "Chile3", "-2 RW", 4, "Hop", 400, -1, 500, 925.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(34, "China", "-7", 16, "Hop", 2000, -1, 250, 920.625, 924.375, ""));
        frequencySet.Add(new FREQUENCYSET(35, "Colombia", "-2 RW", 50, "Hop", 400, -1, 500, 902.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(36, "Congo", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(37, "CostaRica", "-2 RW", 50, "Hop", 400, -1, 500, 902.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(38, "Cotedlvoire", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(39, "Croatia", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(40, "Cuba", "-2 RW", 50, "Hop", 400, -1, 500, 902.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(41, "Cyprus1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(42, "Cyprus2", "-9", 3, "Fixed", 3900, -1, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(43, "Czech1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(44, "Czech2", "-9", 3, "Fixed", 3900, -1, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(45, "Denmark1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(46, "Denmark2", "-9", 3, "Fixed", 3900, -1, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(47, "Dominican", "-2 RW", 50, "Hop", 400, -1, 500, 902.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(48, "Ecuador", "-2 RW", 50, "Hop", 400, -1, 500, 902.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(49, "Egypt", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(50, "ElSalvador", "-2 RW", 50, "Hop", 400, -1, 500, 902.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(51, "Estonia", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(52, "Finland1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(53, "Finland2", "-9", 3, "Fixed", 3900, -1, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(54, "France", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(55, "Georgia", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(56, "Germany", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(57, "Ghana", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(58, "Greece", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(59, "Guatemala", "-2 RW", 50, "Hop", 400, -1, 500, 902.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(60, "HongKong1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(61, "HongKong2", "-2 OFCA", 50, "Hop", 400, -1, 50, 921.25, 923.7, ""));
        frequencySet.Add(new FREQUENCYSET(62, "Hungary1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(63, "Hungary2", "-9", 3, "Fixed", 3900, -1, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(64, "Iceland", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(65, "India", "-1", 3, "Fixed", 3900, 100, 600, 865.7, 866.9, ""));
        frequencySet.Add(new FREQUENCYSET(66, "Indonesia", "-7", 4, "Hop", 400, -1, 500, 923.75, 924.25, ""));
        frequencySet.Add(new FREQUENCYSET(67, "Iran", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(68, "Ireland1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(69, "Ireland2", "-9", 3, "Fixed", 3900, -1, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(70, "Israel", "-9", 3, "Fixed", 3900, -1, 500, 915.5, 916.5, ""));
        frequencySet.Add(new FREQUENCYSET(71, "Italy", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(72, "Jamaica", "-2 RW", 50, "Hop", 400, -1, 500, 902.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(73, "Japan4", "-8 JP4", 4, "Fixed", 3900, -1, 1200, 916.8, 920.4, ""));
        frequencySet.Add(new FREQUENCYSET(74, "Japan6", "-8 JP6", 6, "Fixed", 3900, 100, 1200, 916.8, 920.8, "Channel separation 200 KHz Last 2, LBT carrier sense with Transmission Time Control"));
        frequencySet.Add(new FREQUENCYSET(75, "Jordan", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(76, "Kazakhstan", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(77, "Kenya", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(78, "Korea", "-6	", 6, "Hop", 400, -1, 600, 917.3, 920.3, ""));
        frequencySet.Add(new FREQUENCYSET(79, "KoreaDPR", "-7", 16, "Hop", 400, -1, 250, 920.625, 924.375, ""));
        frequencySet.Add(new FREQUENCYSET(80, "Kuwait", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(81, "Kyrgyz", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(82, "Latvia", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(83, "Lebanon", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(84, "Libya", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(85, "Liechtenstein1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(86, "Liechtenstein2", "-9", 3, "Fixed", 3900, -1, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(87, "Lithuania1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(88, "Lithuania2", "-9", 3, "Fixed", 3900, -1, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(89, "Luxembourg1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(90, "Luxembourg2", "-9", 3, "Fixed", 3900, -1, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(91, "Macao", "-7", 16, "Hop", 400, -1, 250, 920.625, 924.375, ""));
        frequencySet.Add(new FREQUENCYSET(92, "Macedonia", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(93, "Malaysia", "-7", 6, "Hop", 400, -1, 500, 919.75, 922.25, ""));
        frequencySet.Add(new FREQUENCYSET(94, "Malta1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(95, "Malta2", "-9", 3, "Fixed", 3900, -1, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(96, "Mauritius", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(97, "Mexico", "-2", 50, "Hop", 400, -1, 500, 902.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(98, "Moldova1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(99, "Moldova2", "-9", 3, "Fixed", 3900, -1, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(100, "Mongolia", "-7", 16, "Hop", 400, -1, 250, 920.625, 924.375, ""));
        frequencySet.Add(new FREQUENCYSET(101, "Montenegro", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(102, "Morocco", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(103, "Netherlands", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(104, "NewZealand1", "-1", 4, "Hop", 400, -1, 500, 864.75, 867.25, ""));
        frequencySet.Add(new FREQUENCYSET(105, "NewZealand2", "-2 NZ", 14, "Hop", 400, -1, 500, 920.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(106, "Nicaragua", "-2 RW", 50, "Hop", 400, -1, 500, 902.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(107, "Nigeria", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(108, "Norway1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(109, "Norway2", "-9", 3, "Fixed", 3900, -1, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(110, "Oman", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(111, "Pakistan", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(112, "Panama", "-2 RW", 50, "Hop", 400, -1, 500, 902.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(113, "Paraguay", "-2 RW", 50, "Hop", 400, -1, 500, 902.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(114, "Peru", "-2 RW", 24, "Hop", 400, -1, 500, 915.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(115, "Philippines", "-2 RW", 50, "Hop", 400, -1, 500, 902.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(116, "Poland", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(117, "Portugal", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(118, "Romania", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(119, "Russia1", "-1", 4, "Fixed", 3900, 100, 600, 866.3, 867.5, "2 W ERP"));
        frequencySet.Add(new FREQUENCYSET(120, "Russia3", "-9", 4, "Fixed", 3900, -1, 1200, 915.6, 919.2, "1 W ERP"));
        frequencySet.Add(new FREQUENCYSET(121, "Senegal", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(122, "Serbia", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(123, "Singapore1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(124, "Singapore2", "-2 SG", 8, "Hop", 400, -1, 500, 920.75, 924.25, ""));
        frequencySet.Add(new FREQUENCYSET(125, "Slovak1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(126, "Slovak2", "-9", 3, "Fixed", 3900, -1, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(127, "Slovenia1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(128, "Solvenia2", "-9", 3, "Fixed", 3900, 100, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(129, "SAfrica1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(130, "SAfrica2", "-9", 7, "Fixed", 400, -1, 500, 915.7, 918.7, "915.4-919"));
        frequencySet.Add(new FREQUENCYSET(131, "Spain", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(132, "SriLanka", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(133, "Sudan", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(134, "Sweden1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(135, "Sweden2", "-9", 3, "Fixed", 3900, -1, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(136, "Switzerland1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(137, "Switzerland2", "-9", 3, "Fixed", 3900, -1, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(138, "Syria", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(139, "Taiwan1", "-4", 12, "Hop", 400, -1, 375, 922.875, 927.000, "1 Watt ERP for Indoor"));
        frequencySet.Add(new FREQUENCYSET(140, "Taiwan2", "-4", 12, "Hop", 400, -1, 375, 922.875, 927.000, "0.5 Watt ERP for Outdoor"));
        frequencySet.Add(new FREQUENCYSET(141, "Tajikistan", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(142, "Tanzania", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(143, "Thailand", "-2 RW", 8, "Hop", 400, -1, 500, 920.75, 924.25, ""));
        frequencySet.Add(new FREQUENCYSET(144, "Trinidad", "-2 RW", 50, "Hop", 400, -1, 500, 902.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(145, "Tunisia", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(146, "Turkey", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(147, "Turkmenistan", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(148, "Uganda", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(149, "Ukraine", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(150, "UAE", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(151, "UK1", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(152, "UK2", "-9", 3, "Fixed", 3900, -1, 1200, 916.3, 918.7, ""));
        frequencySet.Add(new FREQUENCYSET(153, "USA", "-2", 50, "Hop", 400, -1, 500, 902.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(154, "Uruguay", "-2 RW", 50, "Hop", 400, -1, 500, 902.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(155, "Venezuela", "-2 RW", 50, "Hop", 400, -1, 500, 902.75, 927.25, ""));
        frequencySet.Add(new FREQUENCYSET(156, "Vietnam1", "-1", 4, "Fixed", 3900, 100, 600, 866.7, 868.5, "866-869"));
        frequencySet.Add(new FREQUENCYSET(157, "Vietnam2", "-7", 16, "Hop", 400, -1, 250, 920.625, 924.375, ""));
        frequencySet.Add(new FREQUENCYSET(158, "Yemen", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
        frequencySet.Add(new FREQUENCYSET(159, "Zimbabwe", "-1", 4, "Fixed", 3900, 100, 600, 865.7, 867.5, ""));
    }
}
