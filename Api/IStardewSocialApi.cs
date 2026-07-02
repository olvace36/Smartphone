using System;

namespace Smartphone
{
    public interface IStardewSocialApi
    {
        void CreateDraftPost(string? text = null, string? taggedNpc = null, string? imagePath = null, string? postTags = null);
    }
}
