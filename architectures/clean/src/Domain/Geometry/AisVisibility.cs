namespace CleanArchitecture.Domain.Geometry;

public enum AisVisibility
{
    // The vessel is outside the camera's vertical field of view (data_filter returns None,
    // and the caller neither projects the record nor drops anything).
    OutsideVerticalFov,

    // Inside both fields of view: project the AIS position into image coordinates.
    Transform,

    // Inside the vertical but outside the horizontal field of view ('visTraj_del'):
    // the matching visual track is dropped.
    RemoveVisualTrack,
}
