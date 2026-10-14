$(document).ready(function(){

    // Show only highlights on page load (matches the default selected button)
    $(".filter").not('.highlights').hide();

    $(".filter-button").click(function(){
        var value = $(this).attr('data-filter');

        if (value === "all") {
            $('.filter').show(500);
        } else {
            $(".filter").not('.' + value).hide(500);
            $('.filter').filter('.' + value).show(500);
        }

        $(".filter-button").removeClass('btn-primary');
        $("button[data-filter='" + value + "']").addClass('btn-primary');
    });

});