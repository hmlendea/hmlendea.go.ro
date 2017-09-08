$(function() {
    var selectedClass = "";
    $(".fil-cat").click(function() {
        selectedClass = $(this).attr("data-rel");
        $("#portfolio-gallery").fadeTo(100, 0.1);
        $("#portfolio-gallery .portfolio-item").not("." + selectedClass).fadeOut().removeClass('scale-anm');
        setTimeout(function() {
            $("." + selectedClass).fadeIn().addClass('scale-anm');
            $("#portfolio-gallery").fadeTo(300, 1);
        }, 300);
    });
});