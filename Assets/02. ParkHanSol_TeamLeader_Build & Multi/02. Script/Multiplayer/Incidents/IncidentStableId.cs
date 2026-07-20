using Unity.Collections;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public static class IncidentStableId
    {
        public static bool IsValid(string value)
        {
            if (string.IsNullOrWhiteSpace(value)
                || value[0] < 'a'
                || value[0] > 'z'
                || value[value.Length - 1] == '_')
            {
                return false;
            }

            var previousWasUnderscore = false;
            foreach (var character in value)
            {
                var isLowerLetter = character >= 'a' && character <= 'z';
                var isDigit = character >= '0' && character <= '9';
                var isUnderscore = character == '_';
                if (!isLowerLetter && !isDigit && !isUnderscore)
                {
                    return false;
                }

                if (isUnderscore && previousWasUnderscore)
                {
                    return false;
                }

                previousWasUnderscore = isUnderscore;
            }

            var fixedValue = default(FixedString64Bytes);
            return fixedValue.CopyFrom(value) == CopyError.None;
        }
    }
}
