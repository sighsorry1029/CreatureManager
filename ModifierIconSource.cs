using System;
using System.Collections.Generic;

namespace CreatureManager;

internal enum ModifierIconTone : byte
{
    Clear,
    Tone1,
    Tone2,
    Tone3
}

internal readonly struct ModifierIconColor
{
    internal ModifierIconColor(byte red, byte green, byte blue)
    {
        Red = red;
        Green = green;
        Blue = blue;
    }

    internal byte Red { get; }
    internal byte Green { get; }
    internal byte Blue { get; }
}

internal sealed class ModifierIconSpec
{
    private readonly byte[] _tones;
    private readonly ModifierIconColor[] _palette;

    internal ModifierIconSpec(
        string key,
        string packedToneRuns,
        params ModifierIconColor[] palette)
    {
        if (palette.Length is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(palette));
        }

        Key = key;
        _palette = palette;
        _tones = UnpackTones(packedToneRuns);
    }

    internal string Key { get; }

    internal ModifierIconTone GetTone(int x, int y)
    {
        if ((uint)x >= ModifierIconSource.Size || (uint)y >= ModifierIconSource.Size)
        {
            throw new ArgumentOutOfRangeException();
        }

        return (ModifierIconTone)_tones[y * ModifierIconSource.Size + x];
    }

    internal ModifierIconColor GetColor(ModifierIconTone tone)
    {
        int index = (int)tone - 1;
        if ((uint)index >= (uint)_palette.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(tone));
        }

        return _palette[index];
    }

    private static byte[] UnpackTones(string packedToneRuns)
    {
        byte[] runs = Convert.FromBase64String(packedToneRuns);
        byte[] tones = new byte[ModifierIconSource.Size * ModifierIconSource.Size];
        int destination = 0;
        foreach (byte run in runs)
        {
            byte tone = (byte)(run >> 6);
            int count = (run & 0x3F) + 1;
            if (tone > (byte)ModifierIconTone.Tone3 || destination + count > tones.Length)
            {
                throw new InvalidOperationException("Invalid modifier icon tone data.");
            }

            for (int index = 0; index < count; index++)
            {
                tones[destination++] = tone;
            }
        }

        if (destination != tones.Length)
        {
            throw new InvalidOperationException("Incomplete modifier icon tone data.");
        }

        return tones;
    }
}

internal static class ModifierIconSource
{
    internal const int Size = 64;

    // Each entry contains its modifier key, human-readable RGB palette in tone order,
    // then lossless 2-bit tone-map runs. To adjust geometry, edit the matching
    // docs/modifier-icons PNG and run:
    // powershell -ExecutionPolicy Bypass -File tools/ModifierIconPreview/Regenerate-ModifierIconToneMaps.ps1
    private static readonly ModifierIconSpec[] Icons =
    {
        // Offense
        Icon("enraged", P(C(255, 59, 31)), "Pz8/B0E8QzpFDEAqRwpCKUgIRClIBkYpSARGK0gCRi1IAEYvTjFMM0s0SzRLMk0wTy5RLFMqVShGAU4mRgNOJkQFTiZCB04mQAlOMU4xTjFOMU4xTjFOMU4xTjFOMU4xTjFOMU4xTjFOMU0yTDNMM0s0SjVJNkk4RjxCPz8/Pz8/Pz8/Pz8/Pwo="),
        Icon("fire", P(C(255, 51, 5), C(255, 209, 20)), "Pz8/Px5FNEwwUStVKFclWiNcIU6BTR9NhEweTIdMHUuJTBtLi0wZTIxLGEyNSxhLj0oYS49LFkuQSxZLkEsWS5FKFUyRShVMkUsUTY9ME06PTBNOj0sUTo5MFE6OSxVPjUsWT4tMFlCKSxdQiUwXUYdMGFKFTRlRhUwaUoNMG1OBTRtiHWAeYB9eIVwiXCNaJFolWSZXKFYpVSpTLFItUC9OMU0zSjZHOEY6Qz1APz8/HQ=="),
        Icon("frost", P(C(89, 217, 255)), "Pz8/Pz8/Pz8/PxxFOUU5RTlFLUAKRQpAIEIJRQlCHkQIRQhEHEYHRQdGHEYGRQZGHkYFRQVGIEYERQRGIkYDRQNGJEYCRQJGJkYBRQFGKEYARQBGKlMsUS5PME0iaxNrE2sTaxNrE2siTTBPLlEsUypGAEUARihGAUUBRiZGAkUCRiRGA0UDRiJGBEUERiBGBUUFRh5GBkUGRhxGB0UHRhxECEUIRB5CCUUJQiBACkUKQC1FOUU5RTlFPz8/Pz8/Pz8/Pxw="),
        Icon("lightning", P(C(255, 230, 20)), "Pz8/Pz8/PxdAP0A+QT1CPEI8QzxDO0Q6RDpFOUY5RjhHN0c3SDdINkk1STVKNEs0SzNMJVkmWSZZJVolWSZZJVolWiVaJUszSzRKNUk1STZIN0c3RzhGOUU5RjlFOkQ7QztDPEI9QT1BPkA/Pz8/Pz8/Gw=="),
        Icon("spirit", P(C(255, 173, 13), C(217, 38, 20)), "Pz8/Pz8bRzdHN0c3RzdHN0c3RzdHN0c3RzdHN0c3RzdHN0c3RzdHN0csXSFMg0whS4VLIUuFSyFLhUshS4VLIUyDTCFdKksyTTBPL08uRgNGLUUFRS1FBUUsRQdFK0UHRStFB0UrRQdFK0UHRStFB0UrRQdFK0UHRSxFBUUtRQVFLUYDRi5PL08wTTJLNEk3RT8/Pz8/Pz8/Pxw="),
        Icon("armorPiercing", P(C(255, 133, 20), C(122, 191, 255), C(51, 64, 140)), "Pz8/PwNBPUY5STVODIEhTwiFIE4HiB9NBosfSwSPHkoDkh1KApUbSwCXG0qZGkqaGUQBRJoYQwRCmxdCBYJAh8GRF0AFjMKRHYbAg8SQHIfBgsWDwIoch8KAx4DDihqI0YkZitGJGIrSiBiKyEHHiReJyEPEixeHyUXCjBaHyUePFYfISY4ViMZLjRWKxEyMFYvETIsVjMRMihWLxkyJFYrITIgUispMhxSLykyGFI7ITIYTkMdMhROQxoFMhBOPxoNMgxSOxYVMghSOxIdMgRSPwolMgBSQwItMFZ1MFJ5ME59ME59MEqBMF5tLIpFJL4UARzhFOkM8QT8/Pz8/CQ=="),
        Icon("staggering", P(C(255, 140, 5), C(255, 242, 71), C(255, 219, 31)), "Pz8/Pz8/Pz8/Pz8/EUgyUilYJF0gYB1VgUsaSgpAgU0XSA6BAE4VRg+DAkwURRCDBUsRRhCDB0oQRRCFCEkPRRCFCkgORRCFC0gNRQ+HDEcMRg6HDUcMRQ6HDkcLRQ2JDkYMRQyJD0YLRgmNDkUMRgWTDEULRwGZCUUMRZ8GRgxBpQRFDUClBEUOQp8HRQ9EmQpFEEaTDEYRSI0PRRRIiRBGEcEBR4kOSBHBA0aHQQpKEcIFRIdWEsMGQodVE8MIQYVVE8UJhVMUxwiFUBTMB4MESBbRBIMk0QSDJswIgSrHCoErxQuBLMM7wzvCPcE9wT8/Pz8w"),
        Icon("undodgeable", P(C(255, 245, 230), C(255, 31, 20)), "Pz8/Pz8/PwdOEU4PThFOD04RTgxREVEJURFRCVERUQlFKUUJRSlFCUUpRQlFKUUJRSlFCUUpRQlFE4AURQlFEoITRQlFEYQSRQlFEIYRRQlFD4gQRQlFDooPRR2MMYYAhi+GAoYthgSGK4YBQwCGKYYBRQCGJ4YCRQGGJoYCRQGGJ4YBRQCGKYYBQwCGK4YEhi2GAoYvhgCGMYweRQ6KD0UJRQ+IEEUJRRCGEUUJRRGEEkUJRRKCE0UJRROAFEUJRSlFCUUpRQlFKUUJRSlFCUUpRQlFKUUJURFRCVERUQlREVEMThFOD04RTg9OEU4/Pz8/Pz8/Bw=="),

        // Defense
        Icon("armored", P(C(20, 71, 173)), "Pz8/Pz8eQTpHNE0uUyhZIl8eYR1hHWEcYhxjG2MbYxtjG2MbYxpkGmUZZRllGWUZZRllGGYYZxdnF2cXZxZpFGsSbBJtEG8QbRJrFWcYZRllGmMcYR1hHWEdYR1hHWEdYR1hHUwHTB1KC0ocSg1KGkoPShlJEUkYShFKF0kTSRtFE0QlQBNAPz8/FA=="),
        Icon("deathward", P(C(46, 10, 82), C(92, 31, 158), C(240, 217, 255)), "Pz8/Pz8/PwlrE2sTaxNrE2sTaxNrGKEdoR2hHaEdoR2hHaEdoR2hHYzHjB2Mx4wdjMeMHYzHjB2Mx4wdjMeMHYzHjB2Mx4wdjMeMHYzHjB2H0Ycdh9GHHYfRhx2H0Ycdh9GHHYfRhx2H0Ycdh9GHHYzHjB2Mx4wdjMeMHYzHjB2Mx4wdjMeMHovHix+fH58gnSGdIpskmSWZJpcpkyyRL400hz8/Pz8b"),
        Icon("regenerating", P(C(51, 242, 64)), "Pz8/Pz8/Pz8/GE0xTTFNMU0xTTFNMU0xTTFNMU0xTTFNMU0xTTFNMU0hbhBuEG4QbhBuEG4QbhBuEG4QbhBuEG4QbhBuIE0xTTFNMU0xTTFNMU0xTTFNMU0xTTFNMU0xTTFNMU0xTT8/Pz8/Pz8/GA=="),
        Icon("reflection", P(C(82, 173, 255), C(143, 214, 235), C(245, 255, 255)), "Pz8/Pz8FQARAOEMAQjhHN0g2SzNMM0oUgB5LEoIdTA+FHE0MiBxNCoobTgiMGkMBSQaOGUIDSQWPGUAFSQORIEkCkiBJAJMhSZQhSZMiSZIjRMBDhcCLI0TBQ4HBjCRDwkPCjCVCyY0lQcmNJkHHjiaAQMePJYDJjiSAy40jz4si0YoiRcWQIUfDkR9Jw5EeSoDBkh1KgcGSG0uCwZIaSpkYSwGYF0sCmBZKBZcUSwaXE0oIlxJKCpYQSwuWD0oOkxBKD48VSBGKGkUThh5EFYEjQj8/Pz8/Pz8/NA=="),
        Icon("vortex", P(C(92, 140, 173), C(148, 184, 209)), "Pz8/Pz8/EkU3hUUyiEQxikQvi0QMgx2NQwqFHYxECIZAHI1DCIZAHY1DBohAHotDBohAH4tCBohAIYlCBYlAI4dCBYhBJIZCBYhBJoRCBYhBJ4RABohBGUMKgkEFiEIWSQiBQQWIQhRMB4FABodDE04HgEAFiEITRIlBBoAGh0MSQ41ADIdDE0KQC4VFEkGQDIVFEkGODoNHE0GMEEkVQIwwQIwxQIsyiglBgCeJCUGBBIAhiAhCgQVAgguCEIYIQoIFQJQOggpChAVAlBpDhAVBlBlChQVBlBhDhQVCkxhDhQZCkhhChwVEjkAZQocGRItBGkKIBkeEQxtCiAdNHUKJCEkfQokLQiRCiDNCiTNBiTNCiDRBiDVBhzZBhjdBhTlAgz8/Pz8/IA=="),
        Icon("adaptive", P(C(178, 255, 71), C(235, 255, 148)), "Pz8/Pz8/PxpIMlAuUixUKlYpVihADEk3SAJBF0EZRwBCFUMaShREG0kSRhxIEUcbSQ9JGkoOShxIEEgdRxFHDoAPRRFHDIQPQxFHCogPQRFEAUAJiiFFCo4fRQqOH0UKjh9FCo4fRQqOH0UKjh9FCo4fRQqOCkIRRQqOCkUPRAyKDEQQRA2IDUQQRQ6EDkUQRRCAEEURRSBFEkUgRRNFHkUURhxGFUYaRhZHGEcXRxZHGUUOQQVHG0MNQwNIHUENRAFJH0ALUSxRK1ErUS9NM0c5RTtDPUE/Pz8/Pxs="),
        Icon("unflinching", P(C(255, 158, 28), C(235, 209, 140)), "Pz8/Pz8/Pz8/Pz8/PwtACUA0QQVBkCRCA0KTIUmWHkmYHEmEBo4YgkeAEIsVgkkTiROCSxSIEYFPE4gPgVEUhw2HRxqHDIYCQx2GDIYCQx2GEYAEQR+AHEE9QT8/Pz8/PzeAJoARhiSGDIYkhgyHGUAHQIYNhxhCA0KFD4gWQwFDhBGIFkeEE4kUR4MVixCAR4IYjgaESRyXSx2UTR+QTyGQAkM7QzxBPUE9QT8/Pz8/Pz8/Pz8R"),
        Icon("chameleon", P(C(26, 199, 158), C(209, 255, 46)), "Pz8/Pz8/Pz8/Pz8/Pz8/Pz8/Pz8WQTtDNYdCEUEeikERQxuNQA9FGY5ADkUaj0AMRhmGA4YMRRqEBoVACkUbgwdAhEMGRRyDBkGFTxOEDkGFTgFFDIQOQYVYCoQOQYVZCYUNQYVaCYUMQYVaCYULQYVcCIYKQIZcCYcIhl0KiASIVoFECpUFUINDC5MLSoVCDZARRYVBD40TRoNCEYkWRoFCNEk3RT8/Pz8/Pz8/Pz8/Pz8/Pz8/Cw=="),

        // Affliction
        Icon("exposed", P(C(107, 209, 250), C(184, 245, 255)), "Pz8/Pz8/Pz8bQD1COkQHgDBGBoEtSQaBK0oEhClLBIUnSwaFJUsHhiNLB4ghTAeJH0wHix5LB40cTAeNHEsHjxtLBpAaTQSRGk0EkhlMBpEYTQeQGE4HkBdPB48WUAePFlEHjxVSB44VUwaOFVQEjxVTBY8VUgeOFVEIjhVRB48VUAeQFU8HkRVOB5IVTQiSFU0HkxVOBZQVTgSVFU0GlBVNB5MVTgaTFU4HkhVPB5EVUAaRFVAHkD8/Pz8/Pz8/Pz8/Cg=="),
        Icon("weakened", P(C(245, 173, 61), C(209, 214, 217), C(82, 28, 38)), "Pz8/Pz8/Pz8/Px1BPEM7QjtDgDpCggOAM0KEAYIxQ4kxQosvQ4wuQo0uQowuQ4svQowvQosvQosEQCpCiwVAKEKLB0AnQooIgEAmQooIgUEjQooKgUEiQooKgUIhQYoLgkIfQokNgkIeQokMhEIRwQlBiQyFQhLFA0AAiQuIQhLGBIgKikETxwWECotCFMcGgAyLQRbHE4tBF8YTikEYxhKLQBfIEotAFcoSikAVyxKKQBPNEooSygDDEolAEMoDwhSGEMoFwRaFQA3KB8EYgg7JCcAbgA7IM0PGMkfDM0cAwTNJNUk1STVJNkc3RzlDPz8y"),
        Icon("withered", P(C(168, 199, 56)), "Pz8/Pz8/PxxAOkQ6RBFAJ0QPQidEDkQmRQtGJ0QJSSZEB0smRAVMJ0UCTCpEAUssRAFJLkQCRjBFAUQyRQJCNEQCQDZEOkQ6RTpEOkU5RjhINko1SjRMMk0wUCxCAFApQwBRJkUAUyNGAFUfSAFVHUgCVRxJAlMcSwJSHEwCUBxNBE0dTgRLHVAESR5RBEgfUAZFJE0GRCdLBkQpSQZFK0YHRC1DCEQwQAhEOkQ6QD8/Pz8/Pz8b"),
        Icon("crippling", P(C(199, 115, 255)), "Pz8/Pz8/PxJdIV0hXSFdIV0hXTNGNkguQQNLLFIsUC1PLU8tTi5PL1AvUS1GAkgtQgZILEAJRjlEKkoFSyJICEohSAxIIEcORx9HEEceRxBHHkYSRh5GEkYeRhJGHkYSRh5GEkYeRhJGHkYSRh5GEkYeRxBHHkcQRx9HDkcgSAxIIUgKSCJKBkojWiVYJ1YpVCxQMEw0SD8/Pz8/Pz8/Pxs="),
        Icon("disruptive", P(C(36, 255, 219)), "Pz8/Pz8/Pz8/Pz8/Pz8/GkQWQCBHFEEfSRJCHksQQx1FAkQORA9ADEMGQwxEEEEKQwhDCkQRQghDCkMIRBJDBUQMQwZEE0UBRQ5EAkUVSxBLF0kSSRlHFEccRBZEHkQWRBxHFEcZSRJJF0sQSxVFAUUORAJFFEMFRAxDBkQTQghDCkMIRBJBCkMIQwpEEUAMQwZDDEQeRQJEDkQeSxBDH0kSQiBHFEEiRBZAPz8/Pz8/Pz8/Pz8/Pz8/Pz8/Pz8/Bw=="),
        Icon("adrenalineDrain", P(C(255, 89, 107)), "Pz8/Pz8/Pz8/Pz8vQTxDOkU4RzdHNkk0SzJNME8vTy5RLFMqVShXMEU5RStBC0UnRQtFJ0YKRSdGCkUmSAlFJkkIRSZJCEUlSwdFJUQARQdFJUQBRQZFEUsGRQJREUsGRANREUsAQQNEBFARTgJFBEEATRFPAUQITRFPAUQITRxLEEUdSRFFHUkRRR5IEUUfRhJFH0YSRSBFEkUgQRZFOUU/Pz8/Pz8/Pz8/Pz8L"),
        Icon("corrosive", P(C(184, 199, 207), C(242, 82, 15), C(87, 110, 122)), "Pz8/Pz8/Pz8/PxFNMFiCI1aEIlaFIVaGIVaGIFaHIFaHwB5Xg8OEGk0HQYPDhBlMC4PDhRdLD4HDhRdKEYoWShOKFUkViRRKFYoTSReEQIMSShdFgRNKGUQVShlEFEsZQxVLGUMWShlDFkoZQxZKGUMXSRmCQBdKF4UWSheFF0oVg8OAFUoVg8OBFEsThMOBFUsRhcOAFkwPihdOC4wYTweNGVeFw4MZV4XDHleEwh9XhMIfWIYgWIUgWYQgW4IhXD8/Pz8/Pz8/Pz8U"),
        Icon("toxicDeath", P(C(115, 255, 46)), "Pz8/Pz8/Pz8/PxRVKVUpVSlVKVUpVSlVKVUpVShWJ1glWiNcIU4ATh9OAk4dTgROHE0GTRtNCE0aTQhNGk4GThlQBFAYUQJRGFIAUhhmGGYYSQNKA0kYSAVIBUgYRwdGB0cYRwdGB0cYRwdGB0cYRwdGB0cYSAVIBUgZSANKA0gaZBpkG2IcYh1gH14hXCNaJVgnVipSLk4zSD8/Pz8/Pz8/Gw=="),

        // Special
        Icon("swift", P(C(64, 242, 140)), "Pz8/Pz8/Pz8/Pz8/DEAQQCtCDkIpRAxEJ0cJRyVICEgjSgZKIU0DTR5PAU8eTwFPHk8BTx5QAFAeTwFPHk8BTx5QAFAeTwFPHk8BTx5PAU8eTQNNIUoGSiNICEgkSAhII0oGSiBNA00eTwFPHE8BTxxPAU8bUABQG08BTxxPAU8bUABQG08BTxxPAU8cTwFPHk0DTSBKBkojSAhIJEcJRyZEDEQpQg5CK0AQQD8/Pz8/Pz8/Pz8/Px8="),
        Icon("attackSpeed", P(C(255, 158, 20)), "Pz8/Pz8/Pz8/EVsjWyNbI1sjWyNbJEUNRSRGDUYiSAtIIkgJSCRHCEglSAdHJ0gFSChHBEgqRwJIK0gBRy1HAEguTy9OMUwzSzNKNEo0SjRKNEsyTDFOME8uRwBILEgBRyxHAkgqRwRIKEgFSCZIB0cmRwhIJEgJSCJIC0giRg1GJEUNRSRbI1sjWyNbI1sjWz8/Pz8/Pz8/PxE="),
        Icon("vampiric", P(C(250, 8, 18), C(245, 247, 255), C(235, 240, 255)), "Pz8/Pz8eQTxDOkU4RzdHN0c2STVJK4AISQuAHoAISQuBHIEJRwuCHIIIRwuCHIIJRQyCG4MJRAyDG4MKQwyEGYQLQQyFGYQLQQyFGIUahRiGGIYYhhiHFocYhxaHGIcWhwTDBcMEiBWIA8UDxQOIFYgDxQPFA4kUiQHHAccCiROKAccBxwKJE4oBxwHHAYoTigHHAccBihKLAccBxwGLEYsBxwHHAYsRiwHHAccBixGLAccBxwGLEYsBxwHHAYsQjAHHAccBjA+NAMcBxwCND40AxwHHAI0PjQDHAccAjQ+NAMcBxwCND40AxwHHAI0PjQDHAccAjQ6OAMcBxwCODY0BxwHHAY0NjALHAccCjD8/Pz8/Pz8/Pz8/Pz8/PwY="),
        Icon("reaping", P(C(122, 71, 33), C(92, 20, 41), C(209, 224, 237)), "Pz8/Pz8/LUEhmkQgmEYfl0YhlUYilEYkkkYlkUYmkEYojkYpjkUrjEUsi0UuiUWALohFgS2IRYIth0WELIZFhSuGRYcqhUWIKoRGhyqERogqg0aILIFGiS1GiixGiixGiytGjCpGjSpFjyhFhgGIJ0SFBYclRYQHhiRFhAmFJEQAhAmEJEUAhAmEI0UBhAmEI0QChAmDI0UDhAeEIsFDBIUFhSHDQQWHAYYhxEEGjyDGCI0hxgmLI8YJiSTHCYcmyAiFEcISywaDC8cVzQSBB8sW5hnjG+Ee3yHbKNEyxT8/Hw=="),
        Icon("blink", P(C(148, 92, 255), C(71, 224, 255)), "Pz8/Pz8/Pz8/GkkyTy1TKVclWyJdIEsHSx5JDUkcSBFIG0cTSBpFF0YaRBlGGUQZRhlDC4AORhhCDIEORRhCDIINRhdBDYQMRRdBDYULRRdBDYYKRhCFQJYKRRCFQJgIRRCFQJkHRRCFQJoGRRCFQJoGRRCFQJkHRRCFQJgIRRCFQJYKRRZBDYYKRhZBDYULRRdBDYQMRRdCDIINRhdCDIEORRhDC4AORhhEGUYZRBlGGUUXRhpHE0gaSBFIHEkNSR5LB0sgXSJbJVcpUy1PMkk/Pz8/Pz8/Pz8a"),
        Icon("omen", P(C(199, 125, 255), C(255, 48, 40)), "Pz8/Pz8/Pz8/Pz8/Px5APUI6RjdINEwxTi5SK1QoSgJKJUkGSSJJAoQCSR9IAogCSBxIAowCSBlIA4wDSBZIBI4ESBNHBo4GRxBHB5AHRw1GCZAJRgpHCpAKRwpGCZAJRg1HB5AHRxBHBo4GRxNIBI4ESBZIA4wDSBlIAowCSBxIAogCSB9JAoQCSSJJBkklSgJKKFQrUi5OMUw0SDdGOkI9QD8/Pz8/Pz8/Pz8/Pz8/Hw=="),
        Icon("juggernaut", P(C(209, 184, 122)), "Pz8ZQABFLkADRABLJ1cBQCNYAEMgXx5iG2QZZxZQBlEUTQ9ME0oVSQFAEEYbRwBCD0IhSzJMMUQASC9PLlEsUypKAEgpSgJIJ0oDSCZKBUclSgZFAUAjSghHFEAMSglHE0IKSgpHEkQISgtIEEYGSg1HD0gESg5HDkoCSg9HD0oAShBHEFQRRxFSEkgRUBRHEk4VRxNMFkYVShdFFUwWRxJOFEgLVhNHClkSRwhcEUkFUgBKD0kGUQJKDkgGRQVFBEoORgdEB0QFSA9GBkQJRAVGD0YHRAlEBkQQRQhECUQHQhFECUQJRAhAEkMKRAlEG0MLRAlEG0INRAdEHEINRQVFHEEPTx1AEE8cQBJNM0k3RT8/Py8="),
        Icon("blamer", P(C(87, 56, 18), C(199, 133, 38), C(255, 199, 56)), "Pz8/Pz8/Px5BO0U4RzdHN0c3RzdHKmEdYR1hHIBhgBujFcAFoQXADsEFoQXBDcEFoQXBDMIEwACfAMAEwgrDBMABnQHABMMJwwPBAZ0BwQPDCMQCwgKbAsICxAfEAsICmwLCAsQHwwLDA5kDwwLDBsQCwgSZBMICxAXEAcMEmQTDAcQFwwLCBpcGwgLDBcMCwgaXBsICwwTEAcMGlwbDAcQDxAHDBpcGwwHEA8QBwwaXBsMBxAPEAcMGlwbDAcQDxAHDBpcGwwHEA8QBwwaXBsMBxAPEAsIGlwbCAsQEwwLCBpcGwgLDBcMCwwWXBcMCwwXEAcMFlwXDAcQFxALDBJcEwwLEBsQBwwSXBMMBxAfEAsIFlQXCAsQIwwLCBZUFwgLDCcMDwQWVBcEDwwrCBMAGkwbABMILwg2RDcIMwQ6PDsEOwA+ND8AjhzmDO4M7gzuDO4M/Pz8/Pz8/HQ==")
    };

    private static readonly Dictionary<string, ModifierIconSpec> IconsByKey = BuildIconsByKey();

    internal static IReadOnlyList<ModifierIconSpec> All => Icons;

    internal static ModifierIconSpec Get(string key)
    {
        if (!IconsByKey.TryGetValue(key, out ModifierIconSpec? icon))
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown modifier icon.");
        }

        return icon;
    }

    private static ModifierIconSpec Icon(
        string key,
        ModifierIconColor[] palette,
        string packedToneRuns)
    {
        return new ModifierIconSpec(key, packedToneRuns, palette);
    }

    private static ModifierIconColor[] P(params ModifierIconColor[] palette)
    {
        return palette;
    }

    private static ModifierIconColor C(byte red, byte green, byte blue)
    {
        return new ModifierIconColor(red, green, blue);
    }

    private static Dictionary<string, ModifierIconSpec> BuildIconsByKey()
    {
        Dictionary<string, ModifierIconSpec> result = new(StringComparer.Ordinal);
        foreach (ModifierIconSpec icon in Icons)
        {
            result.Add(icon.Key, icon);
        }

        return result;
    }
}
