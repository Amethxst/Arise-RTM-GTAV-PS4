namespace Arise_RTM
{
    public static class HandlerKeys
    {
        public const string StatSetInt = "stat_set_int";
        public const string StatGetInt = "stat_get_int";
        public const string StatSave = "stat_save";
        public const string StatSetBool = "stat_set_bool";
        public const string StatGetBool = "stat_get_bool";
        public const string StatSetFloat = "stat_set_float";
        public const string StatGetFloat = "stat_get_float";
        public const string PackedSetBool = "packed_set_bool";
        public const string PackedGetBool = "packed_get_bool";
        public const string PackedSetInt = "packed_set_int";

        public static readonly string[] Core = { StatSetInt, StatGetInt, StatSave };
    }
}
